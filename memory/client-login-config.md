# Client Login — Config Pull from Supabase

On login the client does two things, not just version validation:

1. **Version check** — gates access to approved ScoringVersion
2. **User config pull** — downloads scoring preferences for that user

Example config options: "don't deduct points for system check mistakes" (useful for auto-managed aircraft that handle lights/transponder automatically).

This means scoring behavior can vary per user without a client update.
