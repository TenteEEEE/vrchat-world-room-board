using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace RoomBoard
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RoomBoardDisplay : UdonSharpBehaviour
    {
        public RoomBoardManager manager;
        public RoomBoardArea[] areas;
        public string boardTitle = "在室状況";
        public int maxNamesPerArea = 12;
        public float highlightSeconds = 5f;
        public string nameSeparator = "　";
        public string labelEmpty = "空室";
        public string labelCountSuffix = "人";
        public string labelOthersPrefix = "ほか ";
        public string labelOthersSuffix = " 人";
        public string labelUnregistered = "未登録";
        public float textScale = 1f; // Applied in the editor (RoomBoardDisplayEditor); not read at runtime.

        public TextMeshProUGUI titleText;
        public RectTransform rowContainer;
        public GameObject rowTemplate;

        private GameObject[] rowObjects;
        private Image[] accentImages;
        private TextMeshProUGUI[] areaNameTexts;
        private TextMeshProUGUI[] countTexts;
        private TextMeshProUGUI[] namesTexts;
        private RoomBoardArea[] rowAreas;
        private int[] rowAreaIndices;
        private string[] cachedAreaNames;
        private string[] cachedCounts;
        private string[] cachedNames;
        private int lastRevision = -1;
        private float nextHighlightExpiry;

        private void Start()
        {
            if (manager == null) Debug.LogWarning("RoomBoardDisplay: manager is not assigned on " + gameObject.name, this);
            if (titleText != null)
            {
                bool showTitle = boardTitle != null && boardTitle.Length > 0;
                titleText.gameObject.SetActive(showTitle);
                if (showTitle) titleText.text = boardTitle;
            }

            int displayAreaCount = GetDisplayAreaCount();
            rowObjects = new GameObject[displayAreaCount];
            accentImages = new Image[displayAreaCount];
            areaNameTexts = new TextMeshProUGUI[displayAreaCount];
            countTexts = new TextMeshProUGUI[displayAreaCount];
            namesTexts = new TextMeshProUGUI[displayAreaCount];
            rowAreas = new RoomBoardArea[displayAreaCount];
            rowAreaIndices = new int[displayAreaCount];
            cachedAreaNames = new string[displayAreaCount];
            cachedCounts = new string[displayAreaCount];
            cachedNames = new string[displayAreaCount];
            nextHighlightExpiry = float.MaxValue;

            for (int i = 0; i < displayAreaCount; i++)
            {
                if (areas != null && areas.Length > 0)
                {
                    rowAreas[i] = areas[i];
                    rowAreaIndices[i] = manager == null ? -1 : manager.IndexOfArea(areas[i]);
                    if (rowAreaIndices[i] < 0)
                    {
                        string missingName = areas[i] == null ? "(null)" : areas[i].name;
                        Debug.LogWarning("RoomBoardDisplay: area is not registered: " + missingName, this);
                    }
                }
                else
                {
                    rowAreaIndices[i] = manager == null ? -1 : i;
                    rowAreas[i] = manager == null ? null : manager.GetArea(i);
                }

                if (rowTemplate == null || rowContainer == null) continue;
                GameObject row = Instantiate(rowTemplate);
                row.transform.SetParent(rowContainer, false);
                row.SetActive(true);
                rowObjects[i] = row;

                Transform accent = row.transform.Find("Accent");
                Transform areaName = row.transform.Find("AreaName");
                Transform count = row.transform.Find("Count");
                Transform names = row.transform.Find("Names");
                if (accent != null) accentImages[i] = accent.GetComponent<Image>();
                if (areaName != null) areaNameTexts[i] = areaName.GetComponent<TextMeshProUGUI>();
                if (count != null) countTexts[i] = count.GetComponent<TextMeshProUGUI>();
                // richText is not exposed to Udon; the installer enables it on the template.
                if (names != null) namesTexts[i] = names.GetComponent<TextMeshProUGUI>();
            }

            Render();
        }

        private void Update()
        {
            int currentRevision = manager == null ? -1 : manager.GetRevision();
            if (currentRevision != lastRevision || Time.time >= nextHighlightExpiry) Render();
        }

        public void OnPresenceChanged()
        {
            Render();
        }

        private int GetDisplayAreaCount()
        {
            if (areas != null && areas.Length > 0) return areas.Length;
            if (manager == null) return 0;
            return manager.GetAreaCount();
        }

        private void Render()
        {
            lastRevision = manager == null ? -1 : manager.GetRevision();
            nextHighlightExpiry = float.MaxValue;

            if (titleText != null)
            {
                bool showTitle = boardTitle != null && boardTitle.Length > 0;
                titleText.gameObject.SetActive(showTitle);
                if (showTitle && titleText.text != boardTitle) titleText.text = boardTitle;
            }

            if (rowObjects == null) return;
            for (int i = 0; i < rowObjects.Length; i++)
            {
                int areaIndex = rowAreaIndices[i];
                if (manager == null || areaIndex < 0 || areaIndex >= manager.GetAreaCount())
                {
                    SetRowText(i, labelUnregistered, labelUnregistered, "");
                    if (accentImages[i] != null) accentImages[i].color = ColorIdle();
                    continue;
                }

                RoomBoardArea area = rowAreas[i];
                if (area == null) area = manager.GetArea(areaIndex);
                if (area == null)
                {
                    SetRowText(i, labelUnregistered, labelUnregistered, "");
                    if (accentImages[i] != null) accentImages[i].color = ColorIdle();
                    continue;
                }

                int count = manager.GetMemberCount(areaIndex);
                string areaName = area.areaName == null ? "" : area.areaName;
                string countText = count == 0 ? labelEmpty : count + labelCountSuffix;
                string names = BuildNames(areaIndex, count);
                SetRowText(i, areaName, countText, names);
                if (accentImages[i] != null) accentImages[i].color = count > 0 ? area.GetAreaColor() : ColorIdle();
            }
        }

        private void SetRowText(int row, string areaName, string count, string names)
        {
            if (areaName != cachedAreaNames[row])
            {
                cachedAreaNames[row] = areaName;
                if (areaNameTexts[row] != null) areaNameTexts[row].text = areaName;
            }
            if (count != cachedCounts[row])
            {
                cachedCounts[row] = count;
                if (countTexts[row] != null) countTexts[row].text = count;
            }
            if (names != cachedNames[row])
            {
                cachedNames[row] = names;
                if (namesTexts[row] != null) namesTexts[row].text = names;
            }
        }

        private string BuildNames(int areaIndex, int count)
        {
            int maxNames = maxNamesPerArea;
            if (maxNames < 0) maxNames = 0;
            int shown = count < maxNames ? count : maxNames;
            string text = "";
            string separator = nameSeparator == null ? "" : nameSeparator;

            for (int i = 0; i < shown; i++)
            {
                string playerName = manager.GetMemberName(areaIndex, i);
                if (playerName == null) playerName = "";
                playerName = playerName.Replace("<", "<\u200B");

                bool local = manager.GetMemberIsLocal(areaIndex, i);
                bool highlighted = manager.GetMemberHighlight(areaIndex, i)
                    && Time.time - manager.GetMemberEnteredAt(areaIndex, i) < highlightSeconds;
                if (highlighted)
                {
                    playerName = local ? "<color=#F4B942><b>" + playerName + "</b></color>" : "<color=#F4B942>" + playerName + "</color>";
                    float expiry = manager.GetMemberEnteredAt(areaIndex, i) + highlightSeconds;
                    if (expiry < nextHighlightExpiry) nextHighlightExpiry = expiry;
                }
                else if (local)
                {
                    playerName = "<b>" + playerName + "</b>";
                }

                if (i > 0) text += separator;
                text += playerName;
            }

            int others = count - shown;
            if (others > 0)
            {
                if (shown > 0) text += separator;
                text += labelOthersPrefix + others + labelOthersSuffix;
            }
            return text;
        }

        private Color ColorIdle()
        {
            return new Color(0x6B / 255f, 0x7A / 255f, 0x85 / 255f);
        }
    }
}
