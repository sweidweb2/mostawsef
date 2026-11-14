using System;
using System.Collections.Generic;
using System.Text;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>A web page that shows rendered A4 pages and opens the browser's print window as soon as it loads.</summary>
internal static class PrintPage
{
    /// <param name="htmlTitle">The page title, already HTML-encoded.</param>
    /// <param name="pngPages">One PNG per page, in order.</param>
    public static string Build(string htmlTitle, IEnumerable<byte[]> pngPages)
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>").Append(htmlTitle).Append("</title><style>")
            .Append("@page{size:A4;margin:0}")
            .Append("html,body{margin:0;background:#fff}")
            .Append("img{display:block;width:210mm;height:297mm;page-break-after:always}")
            .Append("img:last-child{page-break-after:auto}")
            .Append("@media screen{body{background:#d9d9d9}img{margin:12px auto;box-shadow:0 0 8px rgba(0,0,0,.3)}}")
            .Append("</style></head><body>");

        foreach (var page in pngPages)
        {
            html.Append("<img alt=\"\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(page)).Append("\">");
        }

        html.Append("<script>window.onload=function(){setTimeout(function(){window.print()},300)}</script></body></html>");
        return html.ToString();
    }
}
