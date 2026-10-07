using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

namespace RoomBoard
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RoomBoardManager : UdonSharpBehaviour
    {
        public RoomBoardArea[] areas;
        public float scanInterval = 0.5f;
        public float exitMargin = 0.3f;
        public UdonBehaviour[] listeners;
        public int playersPerFrame = 6;

        private const int MaxPlayers = 128;
        private const int RecentLeaveCount = 8;

        private VRCPlayerApi[] playerBuffer;
        private int[] slotPlayerId;
        private string[] slotName;
        private int[] slotArea;
        private float[] slotEnteredAt;
        private bool[] slotHighlight;
        private bool[] slotIsLocal;
        private int[] slotSweep;
        private int[] occupiedSlots;
        private int occupiedCount;
        private DataDictionary slotByPlayerId;
        private int[] memberSlots;
        private int[] memberPlayerIds;
        private int[] memberCount;
        private int[] nextMemberSlots;
        private int[] nextMemberPlayerIds;
        private int[] nextMemberCount;
        private int[] recentlyLeftIds;
        private float[] recentlyLeftExpiry;
        private int nextRecentLeave;

        private int areaCount;
        private int revision;
        private float scanAccum;
        private bool forceSweep;
        private bool sweepRunning;
        private int sweepId;
        private int sweepPlayerCount;
        private int sweepCursor;
        // A sweep spans several frames; everyone it sees shares one arrival time so ties fall back
        // to playerId order, which keeps the list order identical across clients.
        private float sweepStartedAt;
        private bool membersDirty;
        private bool hasBaseline;
        private const float BaselineSeconds = 3f;
        private float baselineUntil;

        private void Start()
        {
            areaCount = areas == null ? 0 : areas.Length;
            playerBuffer = new VRCPlayerApi[MaxPlayers];
            slotPlayerId = new int[MaxPlayers];
            slotName = new string[MaxPlayers];
            slotArea = new int[MaxPlayers];
            slotEnteredAt = new float[MaxPlayers];
            slotHighlight = new bool[MaxPlayers];
            slotIsLocal = new bool[MaxPlayers];
            slotSweep = new int[MaxPlayers];
            occupiedSlots = new int[MaxPlayers];
            slotByPlayerId = new DataDictionary();
            memberSlots = new int[areaCount * MaxPlayers];
            memberPlayerIds = new int[areaCount * MaxPlayers];
            memberCount = new int[areaCount];
            nextMemberSlots = new int[areaCount * MaxPlayers];
            nextMemberPlayerIds = new int[areaCount * MaxPlayers];
            nextMemberCount = new int[areaCount];
            recentlyLeftIds = new int[RecentLeaveCount];
            recentlyLeftExpiry = new float[RecentLeaveCount];

            for (int i = 0; i < MaxPlayers; i++)
            {
                slotPlayerId[i] = -1;
                slotArea[i] = -1;
            }
            for (int i = 0; i < RecentLeaveCount; i++) recentlyLeftIds[i] = -1;

            SendCustomEventDelayedFrames(nameof(ScanNow), 1);
        }

        private void Update()
        {
            if (playerBuffer == null) return;
            scanAccum += Time.deltaTime;

            if (!sweepRunning)
            {
                if (!forceSweep && scanInterval > 0f && scanAccum < scanInterval) return;
                StartSweep();
            }

            int limit = playersPerFrame;
            if (limit < 1) limit = 1;
            int processed = 0;
            while (sweepCursor < sweepPlayerCount && processed < limit)
            {
                VRCPlayerApi player = playerBuffer[sweepCursor];
                sweepCursor++;
                processed++;
                ProcessPlayer(player);
            }

            if (sweepCursor >= sweepPlayerCount) FinishSweep();
        }

        public void ScanNow()
        {
            forceSweep = true;
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            SendCustomEventDelayedFrames(nameof(ScanNow), 1);
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            int playerId = player.playerId;
            recentlyLeftIds[nextRecentLeave] = playerId;
            recentlyLeftExpiry[nextRecentLeave] = Time.time + 2f;
            nextRecentLeave = (nextRecentLeave + 1) % RecentLeaveCount;

            int slot = FindSlot(playerId);
            if (slot >= 0)
            {
                if (slotArea[slot] >= 0) membersDirty = true;
                ReleaseSlot(slot);
                RebuildIfDirty();
            }
            SendCustomEventDelayedFrames(nameof(ScanNow), 1);
        }

        private void StartSweep()
        {
            forceSweep = false;
            scanAccum = 0f;
            sweepId++;
            VRCPlayerApi.GetPlayers(playerBuffer);
            sweepPlayerCount = VRCPlayerApi.GetPlayerCount();
            if (sweepPlayerCount > MaxPlayers) sweepPlayerCount = MaxPlayers;
            if (sweepPlayerCount < 0) sweepPlayerCount = 0;
            sweepCursor = 0;
            sweepStartedAt = Time.time;
            sweepRunning = true;

            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (Utilities.IsValid(localPlayer) && !hasBaseline)
            {
                hasBaseline = true;
                baselineUntil = Time.time + BaselineSeconds;
            }
        }

        private void ProcessPlayer(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) return;
            int playerId = player.playerId;
            if (IsRecentlyLeft(playerId)) return;

            int slot = FindSlot(playerId);
            if (slot < 0) slot = AllocateSlot(player);
            if (slot < 0) return;

            slotSweep[slot] = sweepId;
            slotName[slot] = player.displayName;
            slotIsLocal[slot] = player.isLocal;

            int currentArea = slotArea[slot];
            int nextArea = FindAreaForPlayer(player, currentArea);
            if (nextArea != currentArea)
            {
                slotArea[slot] = nextArea;
                slotEnteredAt[slot] = sweepStartedAt;
                slotHighlight[slot] = sweepStartedAt >= baselineUntil && nextArea >= 0;
                membersDirty = true;
            }
        }

        private void FinishSweep()
        {
            for (int i = occupiedCount - 1; i >= 0; i--)
            {
                int slot = occupiedSlots[i];
                if (slotSweep[slot] == sweepId) continue;
                if (slotArea[slot] >= 0) membersDirty = true;
                ReleaseSlot(slot);
            }

            sweepRunning = false;
            RebuildIfDirty();
        }

        private bool IsRecentlyLeft(int playerId)
        {
            float now = Time.time;
            for (int i = 0; i < RecentLeaveCount; i++)
            {
                if (recentlyLeftIds[i] != playerId) continue;
                if (now < recentlyLeftExpiry[i]) return true;
                recentlyLeftIds[i] = -1;
            }
            return false;
        }

        private int FindAreaForPlayer(VRCPlayerApi player, int currentArea)
        {
            Vector3 point = player.GetPosition() + Vector3.up * 0.5f;
            int higherLimit = currentArea < 0 ? areaCount : currentArea;

            for (int i = 0; i < higherLimit; i++)
            {
                RoomBoardArea area = areas[i];
                if (area == null) continue;
                if (area.ContainsPoint(point, 0f)) return i;
            }

            if (currentArea >= 0 && currentArea < areaCount)
            {
                RoomBoardArea current = areas[currentArea];
                if (current != null && current.ContainsPoint(point, exitMargin)) return currentArea;
            }

            int laterStart = currentArea < 0 ? 0 : currentArea + 1;
            for (int i = laterStart; i < areaCount; i++)
            {
                RoomBoardArea area = areas[i];
                if (area == null) continue;
                if (area.ContainsPoint(point, 0f)) return i;
            }

            return -1;
        }

        private int FindSlot(int playerId)
        {
            if (slotByPlayerId == null) return -1;
            DataToken value;
            if (!slotByPlayerId.TryGetValue(playerId, out value)) return -1;
            return value.Int;
        }

        private int AllocateSlot(VRCPlayerApi player)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (slotPlayerId[i] >= 0) continue;
                int playerId = player.playerId;
                slotPlayerId[i] = playerId;
                slotName[i] = player.displayName;
                slotArea[i] = -1;
                slotEnteredAt[i] = 0f;
                slotHighlight[i] = false;
                slotIsLocal[i] = player.isLocal;
                slotSweep[i] = 0;
                occupiedSlots[occupiedCount] = i;
                occupiedCount++;
                slotByPlayerId.SetValue(playerId, i);
                return i;
            }
            return -1;
        }

        private void ReleaseSlot(int slot)
        {
            int playerId = slotPlayerId[slot];
            if (playerId >= 0) slotByPlayerId.Remove(playerId);
            slotPlayerId[slot] = -1;
            slotName[slot] = "";
            slotArea[slot] = -1;
            slotEnteredAt[slot] = 0f;
            slotHighlight[slot] = false;
            slotIsLocal[slot] = false;
            slotSweep[slot] = 0;

            for (int i = 0; i < occupiedCount; i++)
            {
                if (occupiedSlots[i] != slot) continue;
                occupiedCount--;
                occupiedSlots[i] = occupiedSlots[occupiedCount];
                occupiedSlots[occupiedCount] = 0;
                break;
            }
        }

        private void RebuildIfDirty()
        {
            if (!membersDirty) return;
            membersDirty = false;
            if (!RebuildMembers()) return;
            revision++;
            NotifyListeners();
        }

        private bool RebuildMembers()
        {
            for (int i = 0; i < areaCount; i++) nextMemberCount[i] = 0;

            for (int occupied = 0; occupied < occupiedCount; occupied++)
            {
                int slot = occupiedSlots[occupied];
                int area = slotArea[slot];
                if (area < 0 || area >= areaCount) continue;

                int count = nextMemberCount[area];
                int insertAt = count;
                while (insertAt > 0)
                {
                    int previousSlot = nextMemberSlots[area * MaxPlayers + insertAt - 1];
                    bool comesBefore = slotEnteredAt[slot] < slotEnteredAt[previousSlot];
                    if (slotEnteredAt[slot] == slotEnteredAt[previousSlot])
                    {
                        comesBefore = slotPlayerId[slot] < slotPlayerId[previousSlot];
                    }
                    if (!comesBefore) break;
                    nextMemberSlots[area * MaxPlayers + insertAt] = previousSlot;
                    nextMemberPlayerIds[area * MaxPlayers + insertAt] = slotPlayerId[previousSlot];
                    insertAt--;
                }
                nextMemberSlots[area * MaxPlayers + insertAt] = slot;
                nextMemberPlayerIds[area * MaxPlayers + insertAt] = slotPlayerId[slot];
                nextMemberCount[area] = count + 1;
            }

            bool changed = false;
            for (int area = 0; area < areaCount; area++)
            {
                int oldCount = memberCount[area];
                int newCount = nextMemberCount[area];
                if (oldCount != newCount) changed = true;

                for (int k = 0; k < newCount; k++)
                {
                    int newPlayerId = nextMemberPlayerIds[area * MaxPlayers + k];
                    if (k >= oldCount || memberPlayerIds[area * MaxPlayers + k] != newPlayerId)
                    {
                        changed = true;
                    }
                }
            }

            if (!changed) return false;

            for (int area = 0; area < areaCount; area++)
            {
                memberCount[area] = nextMemberCount[area];
                for (int k = 0; k < nextMemberCount[area]; k++)
                {
                    int index = area * MaxPlayers + k;
                    memberSlots[index] = nextMemberSlots[index];
                    memberPlayerIds[index] = nextMemberPlayerIds[index];
                }
            }
            return true;
        }

        private void NotifyListeners()
        {
            if (listeners == null) return;
            for (int i = 0; i < listeners.Length; i++)
            {
                UdonBehaviour target = listeners[i];
                if (target == null) continue;
                target.SendCustomEvent("OnPresenceChanged");
            }
        }

        public int GetRevision()
        {
            return revision;
        }

        // Read from the serialized array, not areaCount: a Display's Start can run before ours.
        public int GetAreaCount()
        {
            return areas == null ? 0 : areas.Length;
        }

        public RoomBoardArea GetArea(int areaIndex)
        {
            if (areas == null || areaIndex < 0 || areaIndex >= areas.Length) return null;
            return areas[areaIndex];
        }

        public int IndexOfArea(RoomBoardArea area)
        {
            if (areas == null || area == null) return -1;
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] == area) return i;
            }
            return -1;
        }

        public int GetMemberCount(int areaIndex)
        {
            if (memberCount == null || areaIndex < 0 || areaIndex >= areaCount) return 0;
            return memberCount[areaIndex];
        }

        private int GetMemberSlot(int areaIndex, int k)
        {
            if (memberCount == null || areaIndex < 0 || areaIndex >= areaCount) return -1;
            if (k < 0 || k >= memberCount[areaIndex]) return -1;
            return memberSlots[areaIndex * MaxPlayers + k];
        }

        public string GetMemberName(int areaIndex, int k)
        {
            int slot = GetMemberSlot(areaIndex, k);
            if (slot < 0) return "";
            return slotName[slot];
        }

        public float GetMemberEnteredAt(int areaIndex, int k)
        {
            int slot = GetMemberSlot(areaIndex, k);
            if (slot < 0) return 0f;
            return slotEnteredAt[slot];
        }

        public bool GetMemberHighlight(int areaIndex, int k)
        {
            int slot = GetMemberSlot(areaIndex, k);
            if (slot < 0) return false;
            return slotHighlight[slot];
        }

        public bool GetMemberIsLocal(int areaIndex, int k)
        {
            int slot = GetMemberSlot(areaIndex, k);
            if (slot < 0) return false;
            return slotIsLocal[slot];
        }

        public int GetTotalPresentCount()
        {
            if (memberCount == null) return 0;
            int total = 0;
            for (int i = 0; i < areaCount; i++) total += memberCount[i];
            return total;
        }
    }
}
