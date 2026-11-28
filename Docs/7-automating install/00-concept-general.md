# Continuous delivery of locally installed software — a general concept

An educational overview of how teams move from **manual installer distribution** to **cloud-built releases with in-application update**, especially for products that are more than a single executable.

Companion (product-specific): [`00-concept.md`](00-concept.md)

---

## 1. Context

Many business systems are still **locally installed**: a desktop client, often a local or on-premises service, and a database on the customer’s machine or LAN. Distribution historically meant building an installer, copying it to removable media or a shared drive, and running it with elevated privileges on site.

That model is reliable but does not scale: every improvement requires a human to deliver and run a package. The modern alternative keeps the same **install semantics** (an elevated setup that knows how to place files, register services, and protect data) while changing **how the package arrives** and **who initiates the upgrade**.

---

## 2. Core idea

**Build the installer in continuous integration, publish it with a machine-readable version feed, and let the running application discover, download, verify, and launch that installer — without forcing the user mid-task, and without treating the database as part of the replaceable app folder.**

In other words: automation moves *distribution and discovery*; the trusted installer still owns *machine change*.

---

## 3. Why “desktop-only” updaters are often insufficient

Popular auto-update frameworks typically assume:

> one application directory → replace files → relaunch the process.

That fits a single UI binary. It fits poorly when the product also includes:

- a **background service** that must be stopped and restarted in order,
- **secrets and connection settings** that must survive file replacement,
- a **database engine and data directory** that must never be deleted by an “app update,”
- and a requirement that **client and server components stay version-aligned**.

In those cases, reusing the **full product installer** (often in a silent or unattended mode) is a common first architecture. A custom “folder swap” helper can come later if download size or install time becomes the bottleneck.

---

## 4. Delivery patterns (typology)

| Pattern | Mechanism | Strengths | Trade-offs |
| --- | --- | --- | --- |
| **A — Silent full/update installer** | App downloads the vendor’s setup and runs it elevated/silent | Reuses existing install logic; safest mental model for services + config | Larger downloads; requires Admin/UAC |
| **B — Component package swap** | App downloads a zip/nupkg of app folders; an elevated helper stops services, swaps trees, restores config, restarts | Smaller/faster updates | You own locks, rollback, and partial-failure recovery |
| **C — Client-only update** | Only the UI auto-updates; server is upgraded rarely | Simple if versions may drift | Unsafe when client and API share a strict contract |
| **Manual / offline** | Same installer artifacts on USB or LAN share | Works without Internet | Operational cost; still needed as fallback |

A mature local product often ships **two installer shapes**: a **full** package (first machine, includes heavy dependencies such as a database runtime) and an **update** package (omits those dependencies once they already exist).

---

## 5. Pipeline shape (generic)

```
Develop on a working branch
    → merge to a release branch (intentional ship)
        → CI: automated tests
            → stamp a semantic version
            → build update installer
            → publish installer + version feed (and checksum)
            → create a release record in the artifact store
                → installed clients poll the feed
                    → optional notify → user accepts
                        → download → verify → elevated install → relaunch
```

Essential properties:

- **Release is intentional** (not every development commit).
- **Tests gate** the artifact; a failed gate yields no release.
- The **feed** is small and stable-URL’d (e.g. “latest” metadata), while the **binary** is versioned and immutable.
- The client **compares versions**, ignores corrupt or unreachable feeds, and prefers **notify-only** UX for interactive business software.

---

## 6. The version feed

A minimal feed answers four questions:

1. What is the newest offered **version**?
2. Where is the **artifact**?
3. How do I **integrity-check** it (e.g. SHA-256)?
4. What **human notes** should the UI show (optionally multilingual)?

The application treats “feed newer than installed” as the signal to offer an update. Offline operation must remain possible: no feed means no banner, not a crash.

Security note: if the feed is public and unsigned, **control of the publishing account** is control of what clients may install. Checksums stop casual corruption; they do not replace trust in the publisher. Signing the feed or the package is a later hardening step.

---

## 7. In-application update lifecycle

A careful UX separates phases:

1. **Discover** — poll on a throttle (e.g. after sign-in, not every keystroke).  
2. **Download** — user may keep working; progress and cancel.  
3. **Verify** — checksum (and later, signature) before elevation.  
4. **Install** — close the UI cleanly; run elevated setup; restart services.  
5. **Re-enter** — relaunch; block or wait if the user opens the app mid-install.  
6. **Recover** — on cancel, UAC deny, or setup failure, remain on the previous build.

Policy choices include who may start an update (any user vs administrators only) and whether updates are optional, nagging, or blocking for incompatible builds.

---

## 8. Data and schema safety

Updates replace **software**. They must not casually replace **state**.

- Application config that encodes site identity (keys, connection strings) should be **backed up and restored** across file overwrite.  
- Database files live outside the app tree; installers should **not** delete the data directory.  
- Schema evolution is typically applied when the **new server starts** (migrations), which implies: test migrations before publishing, and keep **backups** before major upgrades.  
- Rollback of binaries does not always undo a forward migration; restore-from-backup remains the last resort.

---

## 9. When the approach fits

Suitable when:

- Customers run **on-premises or single-PC** deployments with Admin available for install.  
- An installer already encodes the hard rules (services, config preserve, leave the DB alone).  
- Internet access is common enough for discovery, with **offline installer** as fallback.  
- The organisation can protect the **release publishing** channel.

Weaker fit when:

- Devices are locked down with no elevation path.  
- Updates must be silent and mandatory without user consent (different product and policy).  
- Artifact size or flaky networks make full-installer downloads unacceptable (favor Pattern B or deltas).  
- Strict air-gapped sites need a different distribution topology (internal mirrors, not public feeds).

---

## 10. Design principles

1. **Separate distribution from mutation** — CI distributes; the installer mutates the machine.  
2. **One shippable version** for components that must stay aligned.  
3. **Failed update leaves a working previous system.**  
4. **State outlives binaries** — database and site config are not “app files.”  
5. **Notify before force** for interactive, mid-shift software.  
6. **Keep an offline path** identical in semantics to the online path.  
7. **Automate after tests** — human judgment chooses *when* to merge to release; machines build and publish.

---

## 11. Vocabulary (for documentation)

| Term | Meaning |
| --- | --- |
| **Feed** | Small metadata document describing the latest offered build |
| **Artifact** | The installer or package clients download |
| **Update package** | Build that upgrades an existing install without re-bundling heavy first-time dependencies |
| **Silent install** | Unattended elevated setup with suppressed UI |
| **Throttle** | Limit how often clients query the feed |
| **Rollback** | Reinstall a previous artifact; may still need data restore if schema moved forward |

---

This framing is product-agnostic: any locally installed, multi-component system can adopt the same pipeline shape, choosing Pattern A or B according to install complexity and update-cost constraints.
