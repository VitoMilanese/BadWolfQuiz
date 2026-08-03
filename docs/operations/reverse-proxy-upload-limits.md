# Reverse-proxy upload limits

Quiz question and answer forms can contain image or audio uploads. Nginx rejects a request before ASP.NET Core sees it when the request exceeds `client_max_body_size`; the browser then receives `413 Request Entity Too Large` and no Razor Page handler runs.

The production Nginx `server` block must therefore allow the same workloads as the application. This project permits quiz package imports up to 1.1 GiB and question-editor requests up to 128 MiB. A compatible Nginx setting is:

```nginx
server {
    server_name badwolf.buzz;
    client_max_body_size 1200M;

    # Existing TLS and proxy configuration...
}
```

After changing the configuration, validate and reload it:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

Nginx may buffer large request bodies to disk. Keep adequate free space in its temporary directory and retain the application's per-file validation; raising the proxy limit does not make every uploaded file valid.

Quiz media is currently stored in SQLite so a database copy and a `.bwquiz` export remain self-contained. Moving media to the filesystem would reduce database size, but it would require coordinated file cleanup, authorization-aware serving, migration, and backups of both the database and the media directory. It should be implemented as a separate storage migration rather than as an upload-limit workaround.
