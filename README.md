# ChristmasKataServer
Server for LSCC Dec 2025

# TODO
- Cache stuff in blob store in case of restart, but in-memory primary for fast lookup
- Add 5 minute throttling
- 5 minutes grace time for sending team names


Auth headers:
"X-Team-Name": team_name,
"X-Unique-Key": team_secret
