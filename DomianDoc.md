Terms

SECS-II	SEMI Equipment Communications Standard — the message format that fab tools speak. Think "the grammar."
HSMS	High-Speed SECS Message Services — SECS-II carried over TCP/IP (older tools used serial/SECS-I). Think "the transport."
GEM	Generic Equipment Model — the behavior rules on top of SECS-II (how a tool reports state, events, alarms).
CEID	Collection Event ID — a number the machine emits when something happens (e.g. CEID 101 = "processing started").
S6F11	A specific SECS message: Stream 6, Function 11 = "Event Report Send." This is the machine saying "an event just occurred, here are its data values."
SVID / DVID / ECID	Status Variable / Data Variable / Equipment Constant IDs — the named data slots attached to an event (temperature, recipe name, wafer count…).
MES	Manufacturing Execution System — the factory's brain. It doesn't care about CEID 101; it cares about states like RUNNING, IDLE, ALARM.

So the entire job of your engine is this one sentence:

Receive raw S6F11 events (full of cryptic CEIDs) from equipment, translate them into meaningful MES states, persist them fast, and stream them live to a dashboard.

Part 2 — Why each architectural choice exists
Equipment → [Receiver] → Channel → [Translator] → ┬→ SQL (audit trail)
                                                   └→ SignalR → Blazor (live UI)
System.Threading.Channels — the machine can fire events faster than the database can write. If the receiver waited on the DB, the TCP socket would back up and you'd miss events from the tool. The Channel is an in-memory queue that decouples "receiving fast" from "processing steadily" (a classic producer/consumer pattern).
Translation as its own worker — mapping logic changes often (new tool, new CEID). Keeping it separate means you never touch the networking code to add a mapping.
Dapper + stored procedures — you want sub-millisecond writes at high volume; a heavy ORM (EF Core change-tracking) would be overhead. Dapper is a thin, fast mapper.
Blazor Server + SignalR — operators need to see state change the instant it happens, pushed from server to browser.