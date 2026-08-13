# SPTushonka.LFS

A self-hosted [Git LFS](https://github.com/git-lfs/git-lfs/blob/main/docs/api/README.md) server. It
implements the batch API and object transfer for one or more repositories and stores objects on
local disk. For GitHub-backed repositories it checks the client's credentials against the real
repository's collaborator permissions, so contributors use their existing GitHub token rather than a
separate account here.

- Any number of repositories in one process, each on its own URL prefix
- Per-repository authentication and visibility
- GitHub permission lookups, cached
- SHA-256 verified on upload; a mismatched object is deleted rather than stored
- Fixed-window rate limiting per client IP

## How it works

Git LFS clients call `<endpoint>/objects/batch` first to ask about a list of objects. The server
replies with an `href` for each one, which the client then `GET`s or `PUT`s:

| Method | Route | Rate limited |
|---|---|---|
| `POST` | `<endpoint>/objects/batch` | yes |
| `GET` | `<endpoint>/objects/{oid}` | yes |
| `PUT` | `<endpoint>/objects/{oid}` | no |
| `GET` | `/health` | no |

Uploads are not rate limited. A single push can carry hundreds of objects, and counting each one
against the limit would get in the way of normal use.

Objects are stored at `data/<endpoint>/<aa>/<bb>/<oid>`. The endpoint path is flattened with
underscores, so `/example-org/example-repo` becomes `example-org_example-repo`, and `aa`/`bb` are
the first two byte-pairs of the object id. Object ids are the SHA-256 of the content, so an object
pushed twice is stored once. Local disk is the only backend
([LocalStorageAdapter.cs](SPTushonka.LFS/StorageAdapters/LocalStorageAdapter.cs)).

## Configuration

`config.jsonc` is read from the working directory, or from the path in `SPT_LFS_CONFIG`. Comments
and trailing commas are allowed. It is read once at startup, so restart after editing. A missing or
invalid file prints the reason to stderr and exits with code 1.

### Endpoints

```jsonc
{
    "RepositoryName": "Example Repository",
    "RepositoryUrl": "https://github.com/example-org/example-repo",
    "Endpoint": "/example-org/example-repo",
    "RepositoryVisibility": "Public",
    "Authentication": "Github"
}
```

| Field | Meaning |
|---|---|
| `RepositoryName` | Label only, used in log lines to tell endpoints apart |
| `RepositoryUrl` | Must be a `github.com` org/repo URL when `Authentication` is `Github`; unused otherwise |
| `Endpoint` | URL prefix. Must start with `/` and must not end with one. It also names the storage directory, so renaming it orphans existing objects |
| `RepositoryVisibility` | `Public` or `Private`. Decides whether reads need credentials; see below |
| `Authentication` | `None`, `Github`, or `Local` |

`RepositoryVisibility` applies to reads. `Public` skips authentication for downloads, covering both
the batch `download` operation and the `GET` that follows it. `Private` asks for credentials on
those too. Uploads always authenticate, so a `Public` endpoint can be read by anyone with the URL
and written to only by someone with push access.

The value is never checked against GitHub. Setting `Public` on a private repository will serve its
LFS objects to anyone, whatever the repository's own settings say. When `Authentication` is `None`
the value makes no difference, since nothing is checked either way.

Authentication modes:

- `Github`: the client sends a GitHub username and personal access token as Basic auth, and the
  server asks the GitHub API what that token can do with the repository. `pull` permission allows
  downloads, `push` allows uploads. Credentials that fail the check get a 401.
- `None`: no checks at all. Use it when nothing untrusted can reach the port.
- `Local`: not implemented. Endpoints using it are logged as an error at startup and skipped.

Bad entries, such as a malformed `Endpoint`, a duplicate, or a `RepositoryUrl` that is not a GitHub
repository, are logged and skipped one by one, leaving the rest running. If none are left, the app
exits with code 1.

### Options

| Option | Default | What it does |
|---|---|---|
| `TrustedProxies` | `[]` | IPs or CIDR ranges allowed to set `X-Forwarded-For` and `X-Forwarded-Proto`. See below |
| `MaxUploadSizeBytes` | `0` | Largest object accepted. `0` or negative is unlimited. Checked in three places: Kestrel's body limit, the batch response so the client hears about it before transferring, and the `PUT` via `Content-Length` |
| `GithubAuthCacheSeconds` | `300` | How long a successful permission lookup is reused. `0` disables the cache and costs a GitHub API call per object per request. Only successes are cached, so revoked access still works for up to this long |
| `RateLimit.PermitLimit` | `10000` | Requests per window per client IP |
| `RateLimit.WindowHours` | `24` | Window length |
| `RateLimit.QueueLimit` | `0` | Requests held once the limit is reached. Anything above `0` makes callers wait for the window to end instead of getting a 429, which with a 24 hour window means holding the connection open for hours |

An empty `TrustedProxies` list accepts forwarded headers from any source, so a client can set its
own `X-Forwarded-For` and get a fresh rate limit bucket whenever it likes. The app logs a warning at
startup if you leave it empty. List the address the reverse proxy connects from: `["127.0.0.1"]` if
the proxy runs on the host, or `["172.16.0.0/12"]` if the app is in Docker, since traffic then
arrives from the bridge gateway rather than loopback. If the address is wrong, `X-Forwarded-Proto`
is dropped along with the rest, `Request.Scheme` falls back to `http`, and the batch API returns
`http://` URLs for objects.

### Logging

The log level is not in `config.jsonc`. Set it through the usual ASP.NET Core `Logging:LogLevel`
section in `appsettings.json`, or an environment variable:

```bash
Logging__LogLevel__SPTushonka.LFS=Debug
```

At `Debug` the app logs the source address of every request along with any `X-Forwarded-For` and
`X-Real-IP` headers. Use it to find the address to put in `TrustedProxies`.

## Running it

The app listens on port 5420. That is set in code, so the ports in `launchSettings.json` have no
effect.

### Docker

[docker-compose.yml](docker-compose.yml) publishes the port on loopback and expects two paths beside
it:

```
config.jsonc      -> mounted read-only at /app/config.jsonc
lfs-data/         -> mounted at /app/data
```

```bash
docker compose up -d
```

Create `config.jsonc` before starting the container for the first time. If it is not there, Docker
creates a directory with that name and the app will not start.

### From source

Needs the .NET 10 SDK.

```bash
dotnet run --project SPTushonka.LFS
```

`config.jsonc` is copied into the output directory on every build, so an edit under `bin/` lasts
until the next build and an edit to the source file needs a build to take effect. Set
`SPT_LFS_CONFIG` to a file outside the project to keep local settings clear of both.

### Behind a reverse proxy

[nginx/reverse_proxy.conf](nginx/reverse_proxy.conf) is a sample config. Besides the forwarding
headers, three settings matter:

- `client_max_body_size 0` removes nginx's 1MB body limit, which otherwise rejects most LFS objects
- `proxy_request_buffering off` streams the upload through instead of spooling it first
- `client_body_timeout 600s` raises the 60s default. With buffering off this timeout applies while
  nginx reads from the client, and a slow push will hit it

The sample only covers port 80. If certbot has set up a TLS vhost, add the same settings to its
`location /` block.

### Pointing a client at it

```bash
git config -f .lfsconfig lfs.url https://lfs.example.com/example-org/example-repo
```

On a `Github` endpoint, sign in with a GitHub username and a personal access token with the `repo`
scope.
