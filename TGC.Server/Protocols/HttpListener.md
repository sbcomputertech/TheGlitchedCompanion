# HTTP listener protocol

This protocol is ised for basic communication with the server, that it **not game related**.

Examples include (with the request type field in brackets):
- Checking if the server is responding (`ping`)
- Getting current game information (`status`)
- Running administrative commands (`admin`)
- Managing player connections (join/leave/etc) (`player`)
- Managing the game world (`world`)

All requests are made to the server address on the BasePort specified either in the `.env` file or by default **9943**.

Requests are made up of JSON data, with one required field `t`. This is the string request type, and can be one of the ones above.
Different types will have their own required fields, and they are all listed further down.

Responses from the server come in a JSON payload, in the form 
```json
{
	"k": true,
	"r": "ack",
	"t": "2023-07-09T00:07:31.4958349+01:00"
}
```
Where:
- `k` is a bool that represents whether the request was successful
- `r` is response data, of which the type depends on the request type
- `t` is the time the server generated the response

If the response is an error (`k` is false), `r` will **always** be a string containing an error message.

---

## Request types

### Ping

A ping is the simplest type of request. 
The client sends a payload only containing the type field (set to `ping` of course),
and the server responds with a JSON payload where `r` is a string that should say `ack`.
This means that everything is OK.

#### Example
Request:
```json
{"t": "ping"}
```

Response:
```json
{
    "k": true,
    "r": "ack",
    "t": "2023-07-09T00:07:31.4958349+01:00"
}
```

### Status
A status request is used to get information about the current game, sort of like a levelled up `ping`.
The client request is the same as ping, just with `status` as the type field.
It always returns an object as `r`, which always has at least two fields:

- `inGame` is a bool, which is true if the server is running a game, or false if it is waiting for players/to start
- `uptime` is a string, showing how long it has been since the server was started. It will be in the form `"00:00:03.6900000"` (unintentional)

If `inGame` is false, it will have the fields:

- `currentPlayers`, int - how many players are currently connected


If `inGame` is true, it will have the fields:

- `currentLevel`, int - the current level being played

#### Example
Request:
```json
{"t": "status"}
```

Response:
```json
{
    "k": true,
    "r": {
        "inGame": false,
        "uptime": "00:00:06.5360000",
        "currentPlayers": 0
    },
    "t": "2023-07-09T01:32:08.3815249+01:00"
}
```

### Admin

# TODO: Finish HTTP protocol docs
