using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.IntegrationTests.Backups;

[Collection(ApiCollection.Name)]
public class BackupsTests(ApiFactory factory)
{
    private const string Url = "api/backups";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<BackupRecordDto> CreateAsync(HttpClient super, string? table, string format)
    {
        var response = await super.PostAsJsonAsync(Url, new CreateBackupRequest { Table = table, Format = format }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BackupRecordDto>(Ct))!;
    }

    private static async Task<byte[]> DownloadAsync(HttpClient super, Guid id)
    {
        var response = await super.GetAsync($"{Url}/{id}/export", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(Ct);
    }

    [Fact]
    public async Task The_table_list_has_the_real_tables_but_no_migrations_table()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var tables = await super.GetJsonAsync<string[]>($"{Url}/tables");

        Assert.Contains("users", tables);
        Assert.Contains("patients", tables);
        Assert.Contains("backup_records", tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Saving_a_backup_stores_only_its_settings_and_shows_in_the_history()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var created = await CreateAsync(super, " patients ", BackupFormats.Json);

        Assert.Equal("patients", created.Table);
        Assert.Equal(BackupFormats.Json, created.Format);
        Assert.Equal(TestUsers.Super.Username, created.CreatedBy);

        var page = await super.GetJsonAsync<PagedResult<BackupRecordDto>>($"{Url}?page=1&pageSize=100");
        Assert.Contains(page.Items, i => i.Id == created.Id && i.Table == "patients");

        var log = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=backups.created");
        Assert.Contains(log.Items, i => i.Username == TestUsers.Super.Username && i.Module == "Backups");
    }

    [Fact]
    public async Task The_history_is_newest_first()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var first = await CreateAsync(super, null, BackupFormats.Csv);
        var second = await CreateAsync(super, null, BackupFormats.Sql);

        var page = await super.GetJsonAsync<PagedResult<BackupRecordDto>>($"{Url}?page=1&pageSize=100");

        var ids = page.Items.Select(i => i.Id).ToList();
        Assert.True(ids.IndexOf(second.Id) < ids.IndexOf(first.Id));
    }

    [Fact]
    public async Task An_unknown_format_or_table_is_a_field_error()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var format = await ProblemResponse.ReadAsync(
            await super.PostAsJsonAsync(Url, new CreateBackupRequest { Format = "Xml" }, Ct));
        var missing = await ProblemResponse.ReadAsync(
            await super.PostAsJsonAsync(Url, new CreateBackupRequest(), Ct));
        var table = await ProblemResponse.ReadAsync(
            await super.PostAsJsonAsync(Url, new CreateBackupRequest { Table = "nope", Format = BackupFormats.Csv }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, format.Status);
        Assert.Equal("invalid", format.Errors["Format"][0]);
        Assert.Equal("required", missing.Errors["Format"][0]);
        Assert.Equal(HttpStatusCode.BadRequest, table.Status);
        Assert.Equal("invalid", table.Errors["Table"][0]);
    }

    [Fact]
    public async Task One_table_as_csv_has_a_header_and_a_utf8_bom()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, "users", BackupFormats.Csv);

        var response = await super.GetAsync($"{Url}/{record.Id}/export", Ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith(".csv", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Id\"".Trim('"'), text.Split("\r\n")[0]);
        Assert.Contains(TestUsers.Super.Username, text);
    }

    [Fact]
    public async Task All_tables_as_csv_is_a_zip_with_one_file_per_table()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var tables = await super.GetJsonAsync<string[]>($"{Url}/tables");
        var record = await CreateAsync(super, null, BackupFormats.Csv);

        using var zip = new ZipArchive(new MemoryStream(await DownloadAsync(super, record.Id)));

        Assert.Equal(tables.Order().Select(t => t + ".csv"), zip.Entries.Select(e => e.FullName).Order());
    }

    [Fact]
    public async Task Json_lists_the_rows_of_each_table()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, "users", BackupFormats.Json);

        using var json = JsonDocument.Parse(await DownloadAsync(super, record.Id));

        var users = json.RootElement.GetProperty("tables").GetProperty("users");
        Assert.Contains(users.EnumerateArray(), u => u.GetProperty("Username").GetString() == TestUsers.Super.Username);
        Assert.Equal(JsonValueKind.False, users[0].GetProperty("IsDeleted").ValueKind);
        Assert.False(json.RootElement.GetProperty("tables").TryGetProperty("patients", out _));
    }

    [Fact]
    public async Task Sql_has_one_insert_per_row_with_quoted_text()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, "users", BackupFormats.Sql);

        var sql = Encoding.UTF8.GetString(await DownloadAsync(super, record.Id));

        Assert.StartsWith("-- Mostawsef backup", sql);
        Assert.Contains("INSERT INTO \"users\" (", sql);
        Assert.Contains($"'{TestUsers.Super.Username}'", sql);
        Assert.Contains("BEGIN;", sql);
        Assert.Contains("COMMIT;", sql);
    }

    [Fact]
    public async Task The_postgresql_format_creates_the_table_and_copies_the_rows()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, "users", BackupFormats.PostgreSql);

        var response = await super.GetAsync($"{Url}/{record.Id}/export", Ct);
        var sql = await response.Content.ReadAsStringAsync(Ct);

        Assert.EndsWith(".pg.sql", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"users\" (", sql);
        Assert.Contains("PRIMARY KEY (\"Id\")", sql);
        Assert.Contains("COPY \"users\" (", sql);
        Assert.Contains("FROM stdin;", sql);
        Assert.Contains("\n\\.\n", sql);
        Assert.Contains(TestUsers.Super.Username, sql);
    }

    [Fact]
    public async Task All_tables_in_sql_put_parent_tables_before_the_tables_that_reference_them()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, null, BackupFormats.PostgreSql);

        var sql = Encoding.UTF8.GetString(await DownloadAsync(super, record.Id));

        Assert.True(sql.IndexOf("-- Table patients", StringComparison.Ordinal) < sql.IndexOf("-- Table orders\n", StringComparison.Ordinal));
        Assert.True(sql.IndexOf("-- Table orders\n", StringComparison.Ordinal) < sql.IndexOf("-- Table order_items", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Exporting_again_uses_the_saved_settings_and_is_audited()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var record = await CreateAsync(super, "users", BackupFormats.Sql);

        var first = Encoding.UTF8.GetString(await DownloadAsync(super, record.Id));
        var second = Encoding.UTF8.GetString(await DownloadAsync(super, record.Id));

        Assert.Contains("INSERT INTO \"users\"", first);
        Assert.Contains("INSERT INTO \"users\"", second);
        Assert.DoesNotContain("INSERT INTO \"patients\"", first);
        var log = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=backups.exported");
        Assert.Contains(log.Items, i => i.Username == TestUsers.Super.Username && i.Module == "Backups");
    }

    [Fact]
    public async Task Exporting_an_unknown_backup_returns_404()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.GetAsync($"{Url}/{Guid.NewGuid()}/export", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_use_any_backup_endpoint()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var responses = new[]
        {
            await admin.GetAsync($"{Url}/tables", Ct),
            await admin.GetAsync(Url, Ct),
            await admin.PostAsJsonAsync(Url, new CreateBackupRequest { Format = BackupFormats.Csv }, Ct),
            await admin.GetAsync($"{Url}/{Guid.NewGuid()}/export", Ct),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Signed_out_users_get_401()
    {
        var anonymous = factory.CreateClient();

        var responses = new[]
        {
            await anonymous.GetAsync($"{Url}/tables", Ct),
            await anonymous.GetAsync(Url, Ct),
            await anonymous.GetAsync($"{Url}/{Guid.NewGuid()}/export", Ct),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }
}
