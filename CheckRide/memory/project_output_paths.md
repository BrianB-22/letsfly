---
name: project_output_paths
description: Where the CheckRide app writes flight results, debug logs, and sample JSON files at runtime
metadata:
  type: project
---

Runtime output goes to `C:\Users\Admin\OneDrive\Documents\CheckRide` — NOT the build output directory.

- Flight result JSON: `C:\Users\Admin\OneDrive\Documents\CheckRide\samples\checkride_YYYYMMDD_HHMMSS.json`
- Debug logs: likely same directory

**Why:** The app uses a user Documents path for output so results persist across rebuilds and are OneDrive-synced.

**How to apply:** Always look here first when reviewing flight results or logs, not in the bin/Debug folder.
