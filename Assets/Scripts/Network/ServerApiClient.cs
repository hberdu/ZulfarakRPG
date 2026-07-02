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
        public string apiBaseUrl = "http://localhost:32770";

        private const string TokenPrefKey = "zulfarak.jwt.token";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private string _accessToken;

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);
        public bool IsReady => IsAuthenticated;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _accessToken = PlayerPrefs.GetString(TokenPrefKey, string.Empty);
        }

        public async Task<bool> AuthenticateWithSteamAsync(string steamId, string playerName)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                Debug.LogWarning("[ServerApi] steamId vazio.");
                return false;
            }

            var req = new SteamAuthRequest
            {
                steamId = steamId,
                playerName = string.IsNullOrWhiteSpace(playerName) ? steamId : playerName
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
                PlayerPrefs.SetString(TokenPrefKey, _accessToken);
                PlayerPrefs.Save();
                Debug.Log("[ServerApi] Auth OK.");
                return true;
            }
            catch (TaskCanceledException)
            {
                Debug.LogWarning("[ServerApi] Timeout na requisição de autenticação.");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerApi] Falha ao autenticar: {e.Message}");
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

        private async Task<string> GetAsync(string path)
        {
            using var req = BuildRequest(HttpMethod.Get, path, includeAuth: true);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> PostAsync(string path, object payload, bool includeAuth)
        {
            using var req = BuildRequest(HttpMethod.Post, path, includeAuth, payload);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<string> PutAsync(string path, object payload)
        {
            using var req = BuildRequest(HttpMethod.Put, path, includeAuth: true, payload);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string path, bool includeAuth, object payload = null)
        {
            var uri = $"{apiBaseUrl.TrimEnd('/')}{path}";
            var req = new HttpRequestMessage(method, uri);

            if (includeAuth && !string.IsNullOrWhiteSpace(_accessToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

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
}
