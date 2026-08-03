# Production OOM Diagnostics

## Scope

An Nginx `502 Bad Gateway` can be a consequence of Kestrel terminating rather
than a proxy failure. When `badwolfquiz.service` is killed by the Linux OOM
killer, use kernel and cgroup evidence to identify the affected process and
invocation before changing memory limits or adding swap.

## Capture the failed invocation

Choose a time range that includes the failure:

```bash
sudo journalctl -k --since "2026-08-03 00:00:00" --until "2026-08-03 01:00:00" \
  -o short-precise | grep -Ei "oom|out of memory|killed process|memory cgroup"

sudo journalctl -u badwolfquiz --since "2026-08-03 00:00:00" \
  --until "2026-08-03 01:00:00" -o short-precise
```

The kernel OOM record is the authoritative source for the killed PID and its
`anon-rss`, `file-rss`, `shmem-rss`, total virtual memory, cgroup path, and
whether the event was a global OOM or a cgroup limit. Preserve these lines
together with the service invocation ID.

## Inspect the current service cgroup

```bash
systemctl show badwolfquiz \
  -p InvocationID -p ExecMainPID -p ControlGroup -p NRestarts \
  -p MemoryAccounting -p MemoryCurrent -p MemoryPeak -p MemoryMax -p MemorySwapMax

CGROUP=$(systemctl show badwolfquiz -p ControlGroup --value)
sudo cat "/sys/fs/cgroup${CGROUP}/memory.current"
sudo cat "/sys/fs/cgroup${CGROUP}/memory.peak"
sudo cat "/sys/fs/cgroup${CGROUP}/memory.events"
sudo cat "/sys/fs/cgroup${CGROUP}/memory.stat"
sudo cat "/sys/fs/cgroup${CGROUP}/memory.swap.current"
sudo cat "/sys/fs/cgroup${CGROUP}/memory.max"
```

`systemctl status` after an automatic restart describes the current service
invocation. Its current and peak values must not be treated as measurements of
the process that was already killed. Likewise, the summary written when an old
invocation exits must be matched by timestamp and invocation ID before it is
compared with the current cgroup. A `65.5M` exit summary and a later `6.2G` peak
therefore do not establish a contradiction by themselves; the complete kernel
OOM record and the invocation-specific cgroup data decide which process held the
memory.

The cgroup `memory.peak` file is reset when the cgroup is recreated. Capture it
before another manual restart whenever possible.

## Application behavior relevant to memory

Active-game recovery embeds the immutable quiz snapshot, including media bytes,
in `App_Data/active-games.json`. Persistence must be revision-driven and use
streaming JSON. Reintroducing unconditional periodic `JsonSerializer.Serialize`
calls would repeatedly allocate Base64 output and a large UTF-16 string on the
large object heap, causing high allocation rates and retained process working
set even when the game is idle.

The `JudgeQuestionAnswer` request mutates only the in-memory runtime state,
broadcasts player and buzzer updates, and redirects to the game page. It does not
query the quiz graph. Quiz rounds, categories, questions, and content blocks are
loaded once by `GameSessionLauncher` using `AsNoTracking` and `AsSplitQuery` when
the game is created.
