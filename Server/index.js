const { WebSocketServer } = require('ws');
const { v4: uuidv4 } = require('uuid');

const PORT = 3000;
const wss = new WebSocketServer({ port: PORT });

// In-memory state (replace with DB for production)
const players = new Map();   // steamId -> { ws, data }
const guilds  = new Map();   // guildId -> guild object
const lobbies = new Map();   // missionId -> { members: Set<steamId>, missionId }

console.log(`ZulfarakRPG server listening on ws://localhost:${PORT}`);

wss.on('connection', (ws) => {
    let steamId = null;

    ws.on('message', (raw) => {
        let msg;
        try { msg = JSON.parse(raw); } catch { return; }

        const payload = msg.payload ? JSON.parse(msg.payload) : {};

        switch (msg.type) {
            case 'player_connect':
                steamId = payload.steamId;
                players.set(steamId, { ws, data: payload });
                console.log(`Player connected: ${steamId}`);
                // Restore guild if exists
                const existingGuild = findGuildByMember(steamId);
                if (existingGuild) send(ws, 'guild_data', existingGuild);
                break;

            case 'guild_create':
                handleGuildCreate(ws, steamId, payload);
                break;

            case 'guild_join':
                handleGuildJoin(ws, steamId, payload);
                break;

            case 'guild_leave':
                handleGuildLeave(steamId);
                break;

            case 'lobby_select_mission':
                handleLobbySelect(steamId, payload.missionId);
                break;

            case 'lobby_ready':
                handleLobbyReady(steamId);
                break;
        }
    });

    ws.on('close', () => {
        if (steamId) {
            players.delete(steamId);
            console.log(`Player disconnected: ${steamId}`);
        }
    });
});

// --- Guild handlers ---

function handleGuildCreate(ws, steamId, payload) {
    if (findGuildByMember(steamId)) {
        send(ws, 'error', { message: 'Already in a guild' });
        return;
    }

    const guild = {
        guildId: uuidv4(),
        guildName: payload.guildName,
        leaderSteamId: steamId,
        memberSteamIds: [steamId],
        maxMembers: 5
    };

    guilds.set(guild.guildId, guild);
    broadcastToGuild(guild.guildId, 'guild_data', guild);
}

function handleGuildJoin(ws, steamId, payload) {
    const guild = guilds.get(payload.guildId);
    if (!guild) { send(ws, 'error', { message: 'Guild not found' }); return; }
    if (guild.memberSteamIds.length >= guild.maxMembers) {
        send(ws, 'error', { message: 'Guild is full' });
        return;
    }
    if (!guild.memberSteamIds.includes(steamId))
        guild.memberSteamIds.push(steamId);

    broadcastToGuild(guild.guildId, 'guild_data', guild);
}

function handleGuildLeave(steamId) {
    const guild = findGuildByMember(steamId);
    if (!guild) return;
    guild.memberSteamIds = guild.memberSteamIds.filter(id => id !== steamId);
    if (guild.memberSteamIds.length === 0) {
        guilds.delete(guild.guildId);
    } else {
        if (guild.leaderSteamId === steamId)
            guild.leaderSteamId = guild.memberSteamIds[0];
        broadcastToGuild(guild.guildId, 'guild_data', guild);
    }
}

// --- Lobby handlers ---

function handleLobbySelect(steamId, missionId) {
    // Remove player from any existing lobby
    for (const [, lobby] of lobbies) lobby.members.delete(steamId);

    if (!lobbies.has(missionId))
        lobbies.set(missionId, { missionId, members: new Set() });

    lobbies.get(missionId).members.add(steamId);
    broadcastLobbyState(missionId);
}

function handleLobbyReady(steamId) {
    for (const [missionId, lobby] of lobbies) {
        if (lobby.members.has(steamId)) {
            if (!lobby.readyMembers) lobby.readyMembers = new Set();
            lobby.readyMembers.add(steamId);

            broadcastLobbyState(missionId);

            if (lobby.readyMembers.size >= 5) {
                const partyIds = [...lobby.readyMembers].slice(0, 5);
                const partyData = partyIds.map(id => players.get(id)?.data).filter(Boolean);

                for (const id of partyIds) {
                    const p = players.get(id);
                    if (p) send(p.ws, 'guild_mission_start', { missionId, partyMembers: partyData });
                }

                lobbies.delete(missionId);
            }
            break;
        }
    }
}

function broadcastLobbyState(missionId) {
    const lobby = lobbies.get(missionId);
    if (!lobby) return;
    const readyIds = lobby.readyMembers ? [...lobby.readyMembers] : [];
    for (const id of lobby.members) {
        const p = players.get(id);
        if (p) send(p.ws, 'lobby_update', { missionId, readyIds });
    }
}

// --- Helpers ---

function findGuildByMember(steamId) {
    for (const [, guild] of guilds)
        if (guild.memberSteamIds.includes(steamId)) return guild;
    return null;
}

function broadcastToGuild(guildId, type, payload) {
    const guild = guilds.get(guildId);
    if (!guild) return;
    for (const id of guild.memberSteamIds) {
        const p = players.get(id);
        if (p) send(p.ws, type, payload);
    }
}

function send(ws, type, payload) {
    if (ws.readyState === ws.OPEN)
        ws.send(JSON.stringify({ type, payload: JSON.stringify(payload) }));
}
