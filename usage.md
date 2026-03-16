# SimLetsFly — Usage Event Tracking

Events are stored in the `page_view` Supabase table via the `increment_page_view` RPC.
All events are anonymous — no personal data is collected.

| `page_name` | When it fires |
|---|---|
| `index` | index.html loads |
| `flights` | flights.html loads |
| `help` | Help/About modal opened (first time per session) |
| `generate_flight` | Successful Let's Fly! result |
| `pull_route` | Successful route pull |
| `export_xp12` | X-Plane 12 sim file downloaded |
| `export_xp11` | X-Plane 11 sim file downloaded |
| `export_msfs2020` | MSFS 2020 sim file downloaded |
| `export_msfs2024` | MSFS 2024 sim file downloaded |
| `save_flight` | Flight saved successfully |
| `share_flight` | Share link copied |
| `flight_brief_copy` | Flight Brief → Copy to Clipboard |
| `flight_brief_save` | Flight Brief → Save as Text File |

## Query

```sql
SELECT page_name, SUM(view_count) AS total
FROM page_view
GROUP BY page_name
ORDER BY total DESC;
```
