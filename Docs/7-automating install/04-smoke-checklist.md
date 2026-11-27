# Update smoke checklist (spare PC)

Run once before trusting in-app updates on a real clinic. Tick as you go.

Related: [`01-release-playbook.md`](01-release-playbook.md) · [`03-data-safety.md`](03-data-safety.md)

## Prepare

- [ ] Spare Windows PC (or VM) with Admin rights — not the live clinic if you can avoid it
- [ ] Old build installed (`Mostawsef-Setup` older than the tag you will publish), signed in works
- [ ] Super → Settings → backup folder set; at least one recent dump exists (or take one)
- [ ] PC can reach GitHub Releases (or you will point a test feed — advanced)

## Publish a newer build

- [ ] Bump `<Version>` in `Directory.Build.props` on `dev` (newer than the installed app version on the login screen)
- [ ] Merge that work to `main` (release-only branch)
- [ ] GitHub Actions **Release** is green; Release page has `Mostawsef-Setup-X.Y.Z.exe` + `latest.json`

## In-app update

- [ ] Open Desktop → sign in (Admin or Super)
- [ ] Banner shows the new version + **Update now**
- [ ] Click **Update now** → progress → UAC → accept
- [ ] App closes; wait window if you reopen early; Desktop comes back
- [ ] Login screen shows the new version number
- [ ] Sign in → patients/orders (or known smoke data) still there
- [ ] `http://localhost:5270/api/health` is healthy (brief offline during restart is OK)

## Failure / offline paths

- [ ] Start Update, then **Cancel** during download → old app still works, no half install
- [ ] Start Update, **deny UAC** → Desktop returns; still on old version; can try again
- [ ] Disconnect network → open app → no crash, no spam; no update banner (or quiet failure)
- [ ] USB / previous Setup still installs over the app if needed (rollback); Postgres data kept

## Sign-off

- [ ] Date / who ran it / tag tested: _______________
- [ ] Notes / issues: _______________
