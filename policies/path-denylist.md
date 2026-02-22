# Operator — Path Denylist

> Some doors stay locked. Operator knows which ones.

---

## Overview

Operator is **never** allowed to modify the following paths in target repos. These are hard boundaries — no override, no exception.

---

## Denied Paths

### Environment & Secrets Files

| Pattern | Reason |
|---|---|
| `.env` | Contains runtime secrets and environment config. Modification could leak or corrupt credentials. |
| `.env.*` | Environment variants (`.env.local`, `.env.production`, etc.). Same risk as `.env`. |

### Certificate & Key Files

| Pattern | Reason |
|---|---|
| `*.key` | Private keys. Exposure breaks encryption and trust chains. |
| `*.pem` | PEM-encoded certificates/keys. Tampering invalidates TLS and signing. |
| `*.p12` | PKCS#12 keystores. Contains bundled certs and keys. |
| `*.pfx` | PFX certificate archives. Same sensitivity as `.p12`. |

### CI/CD Pipeline Configs

| Pattern | Reason |
|---|---|
| `.github/workflows/` (in other repos) | Modifying another repo's CI/CD pipelines could inject arbitrary code execution. |

> **Note:** Operator **CAN** modify its own `.github/workflows/` in the `HoneyDrunk.Operator` repo. This restriction applies only to target repos.

### Secret Directories

| Pattern | Reason |
|---|---|
| `secrets/` | Convention for storing secret material. Off-limits by default. |
| `.secrets/` | Hidden secret directory variant. Same rule applies. |

### Credential Directories

| Pattern | Reason |
|---|---|
| `credentials/` | Stores auth credentials. Operator has no business here. |
| `.credentials/` | Hidden credential directory variant. |

### Secret & Credential Files

| Pattern | Reason |
|---|---|
| `*.secret` | Files explicitly marked as secret by extension. |
| `*.credential` | Files explicitly marked as credential material. |

### Git Internals

| Pattern | Reason |
|---|---|
| `.git/` | Git internal state. Modifying this corrupts repository integrity. |

### Filename Keyword Matches

| Keyword in Filename | Reason |
|---|---|
| `password` | Any file with "password" in its name likely contains or manages credentials. |
| `token` | Token files store auth tokens. Exposure grants unauthorized access. |
| `apikey` | API key files. Same risk as token files. |

---

## Enforcement

- Operator checks every file path against this denylist **before** including it in a PR.
- If a denied path appears in a diff or proposed change, Operator **drops it** and logs a warning.
- Denylist violations are never silently ignored — they surface in Operator logs and PR evidence.

---

## Extending the Denylist

To add new patterns, update this file and merge via standard PR review. The Hive records the change. The Grid respects the boundary.
