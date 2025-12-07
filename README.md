# ChristmasKataServer
Server for LSCC Dec 2025

# TODO
- Add team name/password/score storage
- Store score against team
- Cache stuff in blob store in case of restart, but in-memory primary for fast lookup
- Add 5 minute throttling
- UI endpoint, with throttling status
- scoring endpoint
- 5 minutes grace time for sending team names
- Participant repository should just return the participant, and this should be done at the start of the API call for re-use

Auth headers:
"X-Team-Name": team_name,
"X-Unique-Key": team_secret
