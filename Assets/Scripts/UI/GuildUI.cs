using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZulfarakRPG
{
    public class GuildUI : MonoBehaviour
    {
        [Header("No Guild State")]
        public GameObject noGuildPanel;
        public TMP_InputField createGuildNameInput;
        public Button createGuildButton;
        public TextMeshProUGUI createErrorText;

        [Header("Guild Info State")]
        public GameObject guildInfoPanel;
        public TextMeshProUGUI guildNameText;
        public TextMeshProUGUI memberCountText;
        public Transform memberListContainer;
        public GameObject memberEntryPrefab;
        public Button leaveGuildButton;

        private void Start()
        {
            createGuildButton.onClick.AddListener(TryCreateGuild);
            leaveGuildButton.onClick.AddListener(LeaveGuild);

            GuildManager.Instance.OnGuildCreated += _ => Refresh();
            GuildManager.Instance.OnGuildJoined += _ => Refresh();
            GuildManager.Instance.OnGuildLeft += Refresh;

            Refresh();
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            var guild = GuildManager.Instance.CurrentGuild;
            bool inGuild = guild != null;

            noGuildPanel.SetActive(!inGuild);
            guildInfoPanel.SetActive(inGuild);

            if (!inGuild) return;

            guildNameText.text = guild.guildName;
            memberCountText.text = $"{guild.memberSteamIds.Count}/{guild.maxMembers} membros";

            foreach (Transform child in memberListContainer)
                Destroy(child.gameObject);

            foreach (var id in guild.memberSteamIds)
            {
                var entry = Instantiate(memberEntryPrefab, memberListContainer);
                var label = entry.GetComponentInChildren<TextMeshProUGUI>();
                bool isLeader = guild.IsLeader(id);
                if (label) label.text = isLeader ? $"{id} [Líder]" : id;
            }
        }

        private void TryCreateGuild()
        {
            createErrorText.text = "";
            string name = createGuildNameInput.text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                createErrorText.text = "Digite um nome para a guilda.";
                return;
            }
            if (name.Length < 3 || name.Length > 24)
            {
                createErrorText.text = "O nome deve ter entre 3 e 24 caracteres.";
                return;
            }

            bool ok = GuildManager.Instance.CreateGuild(name);
            if (!ok) createErrorText.text = "Você já está em uma guilda.";
        }

        private void LeaveGuild()
        {
            GuildManager.Instance.LeaveGuild();
        }
    }
}
