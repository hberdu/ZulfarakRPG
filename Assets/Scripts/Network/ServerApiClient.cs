using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ZulfarakRPG
{
    public class ServerApiClient : MonoBehaviour
    {
        public static ServerApiClient Instance { get; private set; }

        [Header("HTTP API")]
        //public string apiBaseUrl = "http://localhost:32772";
        public string apiBaseUrl = "https://zulfarakrpg-production.up.railway.app";

        private const string TokenPrefKey = "zulfarak.jwt.token";
        private const string AdminKeyPrefKey = "zulfarak.admin.key";
        private const string AdminHeaderName = "X-Admin-Key";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private string _accessToken;
        private string _adminKey;
        private bool _sessionAuthenticated;

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);
        public bool IsReady => _sessionAuthenticated && IsAuthenticated;

        // True only on a developer machine that has the catalog admin key set (env var
        // ZULFARAK_ADMIN_KEY or PlayerPrefs). Regular player builds never push the catalog.
        public bool HasAdminKey => !string.IsNullOrWhiteSpace(_adminKey);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _accessToken = PlayerPrefs.GetString(TokenPrefKey, string.Empty);
            _adminKey = Environment.GetEnvironmentVariable("ZULFARAK_ADMIN_KEY");
            if (string.IsNullOrWhiteSpace(_adminKey))
                _adminKey = PlayerPrefs.GetString(AdminKeyPrefKey, string.Empty);
            _sessionAuthenticated = false;
        }

        public async Task<bool> AuthenticateWithSteamAsync(string steamId, string playerName, string sessionTicket = null)
        {
            if (string.IsNullOrWhiteSpace(steamId) && string.IsNullOrWhiteSpace(sessionTicket))
            {
                Debug.LogWarning("[ServerApi] steamId e sessionTicket vazios.");
                return false;
            }

            var req = new SteamAuthRequest
            {
                steamId = steamId,
                playerName = string.IsNullOrWhiteSpace(playerName) ? steamId : playerName,
                sessionTicket = sessionTicket ?? string.Empty
            };

            try
            {
                Debug.Log($"[ServerApi] Tentando autenticar em {apiBaseUrl}");
                var raw = await PostAsync("/api/auth/steam", req, includeAuth: false);
                var auth = JsonConvert.DeserializeObject<AuthResponse>(raw);
                if (auth == null || string.IsNullOrWhiteSpace(auth.accessToken))
                {
                    Debug.LogWarning("[ServerApi] resposta de auth inválida.");
                    return false;
                }

                _accessToken = auth.accessToken;
                _sessionAuthenticated = true;
                PlayerPrefs.SetString(TokenPrefKey, _accessToken);
                PlayerPrefs.Save();
                Debug.Log("[ServerApi] Auth OK.");
                return true;
            }
            catch (TaskCanceledException)
            {
                Debug.LogWarning("[ServerApi] Timeout na requisição de autenticação.");
                _sessionAuthenticated = false;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerApi] Falha ao autenticar: {e.Message}");
                _sessionAuthenticated = false;
                return false;
            }
        }

        public async Task<PlayerData> LoadCharacterAsync()
        {
            var raw = await GetAsync("/api/character/me").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var dto = JsonConvert.DeserializeObject<CharacterDto>(raw);
            return dto?.ToPlayerData();
        }

        public async Task SaveCharacterAsync(PlayerData data)
        {
            if (data == null) return;
            var dto = CharacterDto.FromPlayerData(data);
            await PutAsync("/api/character/me", dto).ConfigureAwait(false);
        }

        public async Task<InventoryStateDto> LoadInventoryAsync()
        {
            var raw = await GetAsync("/api/inventory/me").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return JsonConvert.DeserializeObject<InventoryStateDto>(raw);
        }

        public async Task SaveInventoryAsync(InventoryStateDto state)
        {
            if (state == null) return;
            await PutAsync("/api/inventory/me", state).ConfigureAwait(false);
        }

        public async Task<InventoryStateDto> EquipItemAsync(string itemId)
        {
            var raw = await PostAsync("/api/inventory/me/equip", new EquipItemRequest { itemId = itemId }, includeAuth: true).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<InventoryStateDto>(raw);
        }

        public async Task<InventoryStateDto> UnequipItemAsync(ItemType slotType)
        {
            var raw = await PostAsync("/api/inventory/me/unequip", new UnequipItemRequest { slotType = (int)slotType }, includeAuth: true).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<InventoryStateDto>(raw);
        }

        public async Task<InventoryStateDto> UseConsumableAsync(string itemId)
        {
            var raw = await PostAsync("/api/inventory/me/use", new UseConsumableRequest { itemId = itemId }, includeAuth: true).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<InventoryStateDto>(raw);
        }

        public async Task<ItemDefinitionDto[]> LoadItemDefinitionsAsync()
        {
            var raw = await GetAsync("/api/items/definitions").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<ItemDefinitionDto>();
            }

            var definitions = JsonConvert.DeserializeObject<ItemDefinitionDto[]>(raw);
            return definitions ?? Array.Empty<ItemDefinitionDto>();
        }

        public async Task SyncItemDefinitionsAsync(ItemDefinitionDto[] items)
        {
            if (items == null || items.Length == 0 || !HasAdminKey)
            {
                return;
            }

            var payload = new SyncItemDefinitionsRequest { items = items };
            await PutAsync("/api/items/definitions/sync", payload, includeAdminKey: true).ConfigureAwait(false);
        }

        public async Task SyncEnemyDefinitionsAsync(EnemyDefinitionDto[] enemies)
        {
            if (enemies == null || enemies.Length == 0 || !HasAdminKey)
            {
                return;
            }

            var payload = new SyncEnemyDefinitionsRequest { enemies = enemies };
            await PutAsync("/api/enemies/definitions/sync", payload, includeAdminKey: true).ConfigureAwait(false);
        }

        public async Task<EnemyDefinitionDto[]> LoadEnemyDefinitionsAsync()
        {
            var raw = await GetAsync("/api/enemies/definitions").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<EnemyDefinitionDto>();
            }

            var definitions = JsonConvert.DeserializeObject<EnemyDefinitionDto[]>(raw);
            return definitions ?? Array.Empty<EnemyDefinitionDto>();
        }

        public async Task<CombatResultDto> ResolveCombatAsync(string[] enemyIds)
        {
            if (enemyIds == null || enemyIds.Length == 0)
            {
                return null;
            }

            var payload = new ResolveCombatRequest { enemyIds = enemyIds };
            var raw = await PostAsync("/api/combat/resolve", payload, includeAuth: true).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<CombatResultDto>(raw);
        }

        public async Task<MonsterKillResultDto> RegisterMonsterKillAsync(string enemyId, string enemyName = null)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return null;
            }

            var payload = new RegisterMonsterKillRequest
            {
                enemyId = enemyId.Trim(),
                enemyName = string.IsNullOrWhiteSpace(enemyName) ? string.Empty : enemyName.Trim()
            };
            var raw = await PostAsync("/api/combat/monster-kill", payload, includeAuth: true).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<MonsterKillResultDto>(raw);
        }

        // ── Guilds ───────────────────────────────────────────────────────────
        public async Task<GuildDto> CreateGuildAsync(string name, string description = null)
        {
            var raw = await PostAsync("/api/guild", new CreateGuildRequest { name = name, description = description ?? string.Empty }, includeAuth: true).ConfigureAwait(false);
            return DeserializeOrNull<GuildDto>(raw);
        }

        public async Task<GuildDto> GetMyGuildAsync()
        {
            var raw = await GetAsync("/api/guild/me").ConfigureAwait(false);
            return DeserializeOrNull<GuildDto>(raw);
        }

        public async Task<GuildSummaryDto[]> ListGuildsAsync()
        {
            var raw = await GetAsync("/api/guild").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<GuildSummaryDto>();
            return JsonConvert.DeserializeObject<GuildSummaryDto[]>(raw) ?? Array.Empty<GuildSummaryDto>();
        }

        public async Task<GuildDto> JoinGuildAsync(string guildId)
        {
            var raw = await PostAsync($"/api/guild/{guildId}/join", new { }, includeAuth: true).ConfigureAwait(false);
            return DeserializeOrNull<GuildDto>(raw);
        }

        public async Task LeaveGuildAsync()
        {
            await PostAsync("/api/guild/leave", new { }, includeAuth: true).ConfigureAwait(false);
        }

        public async Task<GuildMissionResultDto> ResolveGuildMissionAsync(string missionId)
        {
            var raw = await PostAsync("/api/guild/mission/resolve", new ResolveGuildMissionRequest { missionId = missionId }, includeAuth: true).ConfigureAwait(false);
            return DeserializeOrNull<GuildMissionResultDto>(raw);
        }

        public async Task SyncGuildMissionsAsync(GuildMissionDefinitionDto[] missions)
        {
            if (missions == null || missions.Length == 0 || !HasAdminKey) return;
            await PutAsync("/api/guild/missions/sync", new SyncGuildMissionsRequest { missions = missions }, includeAdminKey: true).ConfigureAwait(false);
        }

        private static T DeserializeOrNull<T>(string raw) where T : class
            => string.IsNullOrWhiteSpace(raw) ? null : JsonConvert.DeserializeObject<T>(raw);

        private async Task<string> GetAsync(string path)
        {
            using var req = BuildRequest(HttpMethod.Get, path, includeAuth: true);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!res.IsSuccessStatusCode)
            {
                var errorBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}: {errorBody}");
            }
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> PostAsync(string path, object payload, bool includeAuth)
        {
            using var req = BuildRequest(HttpMethod.Post, path, includeAuth, payload);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errorBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}: {errorBody}");
            }
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> PutAsync(string path, object payload, bool includeAdminKey = false)
        {
            using var req = BuildRequest(HttpMethod.Put, path, includeAuth: true, payload, includeAdminKey);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errorBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}: {errorBody}");
            }
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string path, bool includeAuth, object payload = null, bool includeAdminKey = false)
        {
            var uri = $"{apiBaseUrl.TrimEnd('/')}{path}";
            var req = new HttpRequestMessage(method, uri);

            if (includeAuth && !string.IsNullOrWhiteSpace(_accessToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            if (includeAdminKey && !string.IsNullOrWhiteSpace(_adminKey))
                req.Headers.Add(AdminHeaderName, _adminKey);

            if (payload != null)
            {
                var json = JsonConvert.SerializeObject(payload);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return req;
        }
    }

    [Serializable]
    public class SteamAuthRequest
    {
        public string steamId;
        public string playerName;
        public string sessionTicket;
    }

    [Serializable]
    public class AuthResponse
    {
        public string accessToken;
        public string expiresAtUtc;
    }

    [Serializable]
    public class CharacterDto
    {
        public string playerName;
        public int sex;
        public int skinTone;
        public int hairStyle;
        public int hairColorIndex;
        public int faceStyle;
        public int classType;
        public int subclassType;
        public bool subclassUnlocked;
        public int level;
        public long currentExp;
        public long expToNextLevel;
        public long gold;
        public int hp;
        public int maxHp;
        public int attack;
        public int defense;
        public float speed;
        public float healPower;
        public string guildId;
        public bool isGuildLeader;
        public string currentCity;

        public static CharacterDto FromPlayerData(PlayerData data) => new CharacterDto
        {
            playerName = data.playerName,
            sex = (int)data.sex,
            skinTone = (int)data.skinTone,
            hairStyle = (int)data.hairStyle,
            hairColorIndex = data.hairColorIndex,
            faceStyle = (int)data.faceStyle,
            classType = (int)data.classType,
            subclassType = (int)data.subclassType,
            subclassUnlocked = data.subclassUnlocked,
            level = data.level,
            currentExp = data.currentExp,
            expToNextLevel = data.expToNextLevel,
            gold = data.gold,
            hp = data.hp,
            maxHp = data.maxHp,
            attack = data.attack,
            defense = data.defense,
            speed = data.speed,
            healPower = data.healPower,
            guildId = data.guildId,
            isGuildLeader = data.isGuildLeader,
            currentCity = data.currentCity
        };

        public PlayerData ToPlayerData() => new PlayerData
        {
            playerName = playerName,
            sex = (CharacterSex)sex,
            skinTone = (SkinTone)skinTone,
            hairStyle = (HairStyle)hairStyle,
            hairColorIndex = hairColorIndex,
            faceStyle = (FaceStyle)faceStyle,
            classType = (ClassType)classType,
            subclassType = (SubclassType)subclassType,
            subclassUnlocked = subclassUnlocked,
            level = level,
            currentExp = currentExp,
            expToNextLevelServer = expToNextLevel > 0 ? expToNextLevel : PlayerData.CalculateExpToNextLevel(level),
            gold = gold,
            hp = hp,
            maxHp = maxHp,
            attack = attack,
            defense = defense,
            speed = speed,
            healPower = healPower,
            guildId = guildId,
            isGuildLeader = isGuildLeader,
            currentCity = string.IsNullOrWhiteSpace(currentCity) ? "Zulfarak" : currentCity
        };
    }

    [Serializable]
    public class InventoryStateDto
    {
        public InventoryItemDto[] items = Array.Empty<InventoryItemDto>();
        public EquipmentDto equipment = new EquipmentDto();
        public CharacterStatsDto characterStats;

        public static InventoryStateDto FromInventory(Inventory inventory) => new InventoryStateDto
        {
            items = inventory.Items.ConvertAll(x => new InventoryItemDto
            {
                itemId = x.itemId,
                quantity = x.quantity
            }).ToArray(),
            equipment = EquipmentDto.FromEquipment(inventory.Equipment)
        };
    }

    [Serializable]
    public class InventoryItemDto
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class EquipmentDto
    {
        public string weaponId;
        public string helmetId;
        public string chestId;
        public string legsId;
        public string bootsId;
        public string glovesId;
        public string ringId;
        public string amuletId;

        public static EquipmentDto FromEquipment(Equipment eq) => new EquipmentDto
        {
            weaponId = eq.weaponId,
            helmetId = eq.helmetId,
            chestId = eq.chestId,
            legsId = eq.legsId,
            bootsId = eq.bootsId,
            glovesId = eq.glovesId,
            ringId = eq.ringId,
            amuletId = eq.amuletId
        };

        public Equipment ToEquipment() => new Equipment
        {
            weaponId = weaponId,
            helmetId = helmetId,
            chestId = chestId,
            legsId = legsId,
            bootsId = bootsId,
            glovesId = glovesId,
            ringId = ringId,
            amuletId = amuletId
        };
    }

    [Serializable]
    public class CharacterStatsDto
    {
        public int hp;
        public int maxHp;
        public int attack;
        public int defense;
        public float speed;
        public float healPower;
    }

    [Serializable]
    public class ItemDefinitionDto
    {
        public string itemId;
        public string name;
        public string description;
        public int itemType;
        public int rarity;
        public int requiredLevel;
        public int goldValue;
        public int maxStack = 1;
        public int bonusHp;
        public int bonusAttack;
        public int bonusDefense;
        public float bonusSpeed;
        public float bonusHealPower;
        public int[] allowedClassTypes = Array.Empty<int>();

        public static ItemDefinitionDto FromItemData(ItemData item) => new ItemDefinitionDto
        {
            itemId = item.itemId,
            name = item.itemName,
            description = item.description ?? string.Empty,
            itemType = (int)item.itemType,
            rarity = (int)item.rarity,
            requiredLevel = item.requiredLevel,
            goldValue = item.goldValue,
            maxStack = item.itemType == ItemType.Consumable ? 999 : 1,
            bonusHp = item.bonusHp,
            bonusAttack = item.bonusAttack,
            bonusDefense = item.bonusDefense,
            bonusSpeed = item.bonusSpeed,
            bonusHealPower = item.bonusHealPower,
            allowedClassTypes = item.allowedClasses == null
                ? Array.Empty<int>()
                : Array.ConvertAll(item.allowedClasses, x => (int)x)
        };
    }

    [Serializable]
    public class SyncItemDefinitionsRequest
    {
        public ItemDefinitionDto[] items = Array.Empty<ItemDefinitionDto>();
    }

    [Serializable]
    public class EquipItemRequest
    {
        public string itemId;
    }

    [Serializable]
    public class UnequipItemRequest
    {
        public int slotType;
    }

    [Serializable]
    public class UseConsumableRequest
    {
        public string itemId;
    }

    [Serializable]
    public class EnemyDefinitionDto
    {
        public string enemyId;
        public string name;
        public int hp;
        public int attack;
        public int defense;
        public float attackSpeed;
        public int expReward;
        public int goldReward;
        public bool isBoss;
        public EnemyDropDto[] drops = Array.Empty<EnemyDropDto>();

        public static EnemyDefinitionDto FromEnemyData(EnemyData enemy) => new EnemyDefinitionDto
        {
            enemyId = enemy.GetServerEnemyId(),
            name = enemy.enemyName,
            hp = enemy.hp,
            attack = enemy.attack,
            defense = enemy.defense,
            attackSpeed = enemy.attackSpeed,
            expReward = enemy.expReward,
            goldReward = enemy.goldReward,
            isBoss = enemy.isBoss,
            drops = enemy.possibleDrops == null
                ? Array.Empty<EnemyDropDto>()
                : Array.ConvertAll(enemy.possibleDrops, x => EnemyDropDto.FromItemReward(x))
        };
    }

    [Serializable]
    public class EnemyDropDto
    {
        public string itemId;
        public double dropChance;
        public int minQuantity = 1;
        public int maxQuantity = 1;

        public static EnemyDropDto FromItemReward(ItemReward reward)
        {
            var itemId = reward != null ? reward.itemId : null;
            if (string.IsNullOrWhiteSpace(itemId) && reward != null)
            {
                itemId = reward.itemName;
            }

            var qty = reward == null ? 1 : Math.Max(1, reward.quantity);
            return new EnemyDropDto
            {
                itemId = itemId ?? string.Empty,
                dropChance = reward == null ? 0 : Math.Max(0d, Math.Min(1d, reward.dropChance)),
                minQuantity = qty,
                maxQuantity = qty
            };
        }
    }

    [Serializable]
    public class SyncEnemyDefinitionsRequest
    {
        public EnemyDefinitionDto[] enemies = Array.Empty<EnemyDefinitionDto>();
    }

    [Serializable]
    public class ResolveCombatRequest
    {
        public string[] enemyIds = Array.Empty<string>();
    }

    [Serializable]
    public class CombatResultDto
    {
        public bool victory;
        public int killCount;
        public int playerRemainingHp;
        public long expGained;
        public long goldGained;
        public CombatDropResultDto[] drops = Array.Empty<CombatDropResultDto>();
        public InventoryStateDto inventory;
        public CharacterDto character;
    }

    [Serializable]
    public class CombatDropResultDto
    {
        public string itemId;
        public int quantity;
    }

    [Serializable]
    public class RegisterMonsterKillRequest
    {
        public string enemyId;
        public string enemyName;
    }

    [Serializable]
    public class MonsterKillResultDto
    {
        public string enemyId;
        public long expGained;
        public long goldGained;
        public CombatDropResultDto[] drops = Array.Empty<CombatDropResultDto>();
        public InventoryStateDto inventory;
        public CharacterDto character;
    }

    // ── Guild DTOs ───────────────────────────────────────────────────────────
    [Serializable]
    public class CreateGuildRequest
    {
        public string name;
        public string description;
    }

    [Serializable]
    public class GuildMemberDto
    {
        public string steamId;
        public string name;
        public int level;
        public int classType;
        public int subclassType;
        public bool isLeader;
    }

    [Serializable]
    public class GuildDto
    {
        public string id;
        public string name;
        public string description;
        public string leaderSteamId;
        public int level;
        public long exp;
        public int maxMembers;
        public int memberCount;
        public GuildMemberDto[] members = Array.Empty<GuildMemberDto>();
    }

    [Serializable]
    public class GuildSummaryDto
    {
        public string id;
        public string name;
        public int level;
        public int memberCount;
        public int maxMembers;
        public string leaderSteamId;
    }

    [Serializable]
    public class GuildMissionDefinitionDto
    {
        public string missionId;
        public string name;
        public int requiredLevel = 1;
        public int requiredPlayers = 1;
        public long expReward;
        public long goldReward;
        public double baseSuccessChance = 0.5;
        public double tankBonus = 0.10;
        public double healerBonus = 0.15;
        public double dpsBonus = 0.05;
    }

    [Serializable]
    public class SyncGuildMissionsRequest
    {
        public GuildMissionDefinitionDto[] missions = Array.Empty<GuildMissionDefinitionDto>();
    }

    [Serializable]
    public class ResolveGuildMissionRequest
    {
        public string missionId;
    }

    [Serializable]
    public class GuildMissionResultDto
    {
        public string missionId;
        public bool victory;
        public double successChance;
        public long expPerMember;
        public long goldPerMember;
        public int membersRewarded;
        public GuildDto guild;
    }
}
