using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using LMeter.Act;
using LMeter.Act.DataStructures;
using LMeter.Config;
using LMeter.Helpers;
using Newtonsoft.Json;

namespace LMeter.Meter
{
    public class MeterWindow : IConfigurable
    {
        [JsonIgnore] private bool _lastFrameWasUnlocked = false;
        [JsonIgnore] private bool _lastFrameWasDragging = false;
        [JsonIgnore] private bool _lastFrameWasPreview = false;
        [JsonIgnore] private bool _lastFrameWasCombat = false;
        [JsonIgnore] private bool _unlocked = false;
        [JsonIgnore] private bool _hovered = false;
        [JsonIgnore] private bool _dragging = false;
        [JsonIgnore] private bool _locked = false;
        [JsonIgnore] private int _eventIndex = -1;
        [JsonIgnore] private ActEvent? _previewEvent = null;
        [JsonIgnore] private int _scrollPosition = 0;
        [JsonIgnore] private DateTime? _lastSortedTimestamp = null;
        [JsonIgnore] private List<Combatant> _lastSortedCombatants = [];

        [JsonIgnore] public string ID { get; init; }

        public string Name { get; set; }

        public GeneralConfig GeneralConfig { get; set; }

        public HeaderConfig HeaderConfig { get; set; }

        public BarConfig BarConfig { get; set; }

        public BarColorsConfig BarColorsConfig { get; set; }

        public VisibilityConfig VisibilityConfig { get; set; }

        public VisibilityConfig2 VisibilityConfig2 { get; set; }

        public MeterWindow(string name)
        {
            this.Name = name;
            this.ID = $"LMeter_MeterWindow_{Guid.NewGuid()}";
            this.GeneralConfig = new GeneralConfig();
            this.HeaderConfig = new HeaderConfig();
            this.BarConfig = new BarConfig();
            this.BarColorsConfig = new BarColorsConfig();
            this.VisibilityConfig = new VisibilityConfig();
            this.VisibilityConfig2 = new VisibilityConfig2();
        }

        public IEnumerable<IConfigPage> GetConfigPages()
        {
            yield return this.GeneralConfig;
            yield return this.HeaderConfig;
            yield return this.BarConfig;
            yield return this.BarColorsConfig;
            yield return this.VisibilityConfig2;
        }

        public void ImportPage(IConfigPage page)
        {
            switch (page)
            {
                case GeneralConfig newPage:
                    this.GeneralConfig = newPage;
                    break;
                case HeaderConfig newPage:
                    this.HeaderConfig = newPage;
                    break;
                case BarConfig newPage:
                    this.BarConfig = newPage;
                    break;
                case BarColorsConfig newPage:
                    this.BarColorsConfig = newPage;
                    break;
                case VisibilityConfig newPage:
                    this.VisibilityConfig = newPage;
                    break;
            }
        }

        public static MeterWindow GetDefaultMeter(string name)
        {
            MeterWindow newMeter = new(name);
            newMeter.ImportPage(newMeter.HeaderConfig.GetDefault());
            newMeter.ImportPage(newMeter.BarConfig.GetDefault());
            return newMeter;
        }

        public void Clear()
        {
            _lastSortedCombatants = [];
            _lastSortedTimestamp = null;
        }

        // Dont ask
        protected void UpdateDragData(Vector2 pos, Vector2 size, bool locked)
        {
            _unlocked = !locked;
            _hovered = ImGui.IsMouseHoveringRect(pos, pos + size);
            _dragging = _lastFrameWasDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            _locked = (_unlocked && !_lastFrameWasUnlocked || !_hovered) && !_dragging;
            _lastFrameWasDragging = _hovered || _dragging;
        }

        public void Draw(Vector2 pos)
        {
            if (!this.GeneralConfig.Preview && !this.VisibilityConfig2.IsVisible())
            {
                return;
            }

            Vector2 localPos = pos + this.GeneralConfig.Position;
            Vector2 size = this.GeneralConfig.Size;

            if (ImGui.IsMouseHoveringRect(localPos, localPos + size))
            {
                _scrollPosition -= (int)ImGui.GetIO().MouseWheel;

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !this.GeneralConfig.Preview)
                {
                    ImGui.OpenPopup($"{this.ID}_ContextMenu", ImGuiPopupFlags.MouseButtonRight);
                }
            }

            if (this.DrawContextMenu($"{this.ID}_ContextMenu", out int index))
            {
                _eventIndex = index;
                _lastSortedTimestamp = null;
                _lastSortedCombatants = [];
                _scrollPosition = 0;
            }
            else if (this.VisibilityConfig2.ShouldClip &&
                    Singletons.Get<ClipRectsHelper>().GetClipRectForArea(localPos, size).HasValue)
            {
                return;
            }

            bool combat = CharacterState.IsInCombat();
            if (this.GeneralConfig.ReturnToCurrent && !_lastFrameWasCombat && combat)
            {
                _eventIndex = -1;
            }

            this.UpdateDragData(localPos, size, this.GeneralConfig.Lock);
            bool needsInput = !this.GeneralConfig.ClickThrough;
            DrawHelpers.DrawInWindow($"##{this.ID}", localPos, size, needsInput, _locked || this.GeneralConfig.Lock, (drawList) =>
            {
                if (_unlocked)
                {
                    if (_lastFrameWasDragging)
                    {
                        localPos = ImGui.GetWindowPos();
                        this.GeneralConfig.Position = localPos - pos;

                        size = ImGui.GetWindowSize();
                        this.GeneralConfig.Size = size;
                    }
                }

                if (this.GeneralConfig.ShowBorder)
                {
                    Vector2 borderPos = localPos;
                    Vector2 borderSize = size;
                    if (this.GeneralConfig.BorderAroundBars &&
                        this.HeaderConfig.ShowHeader)
                    {
                        borderPos = borderPos.AddY(this.HeaderConfig.HeaderHeight);
                        borderSize = borderSize.AddY(-this.HeaderConfig.HeaderHeight);
                    }

                    for (int i = 0; i < this.GeneralConfig.BorderThickness; i++)
                    {
                        Vector2 offset = new(i, i);
                        drawList.AddRect(borderPos + offset, borderPos + borderSize - offset, this.GeneralConfig.BorderColor.Base);
                    }

                    localPos += Vector2.One * this.GeneralConfig.BorderThickness;
                    size -= Vector2.One * this.GeneralConfig.BorderThickness * 2;
                }

                if (this.GeneralConfig.Preview && !_lastFrameWasPreview)
                {
                    _previewEvent = ActEvent.GetTestData();
                }

                ActEvent? actEvent = this.GeneralConfig.Preview ? _previewEvent : Singletons.Get<LogClient>().GetEvent(_eventIndex);

                (localPos, size) = this.HeaderConfig.DrawHeader(localPos, size, actEvent?.Encounter, drawList);
                drawList.AddRectFilled(localPos, localPos + size, this.GeneralConfig.BackgroundColor.Base);
                this.DrawBars(drawList, localPos, size, actEvent);
            });

            _lastFrameWasUnlocked = _unlocked;
            _lastFrameWasPreview = this.GeneralConfig.Preview;
            _lastFrameWasCombat = combat;
        }

        private void DrawBars(ImDrawListPtr drawList, Vector2 localPos, Vector2 size, ActEvent? actEvent)
        {
            if (actEvent?.Combatants is not null && actEvent.Combatants.Count != 0)
            {
                // We don't want to corrupt the cache. The entire logic past this point mutates the sorted Act combatants instead of using a rendering cache
                // This has the issue that some settings can't behave properly and or don't update till the following combat update/fight
                List<Combatant> sortedCombatants = [.. this.GetSortedCombatants(actEvent, this.GeneralConfig.DataType)];

                float top = this.GeneralConfig.DataType switch
                {
                    MeterDataType.Damage => sortedCombatants[0].DamageTotal?.Value ?? 0,
                    MeterDataType.Healing => sortedCombatants[0].EffectiveHealing?.Value ?? 0,
                    MeterDataType.DamageTaken => sortedCombatants[0].DamageTaken?.Value ?? 0,
                    _ => 0
                };

                int currentIndex = 0;
                string playerName = Singletons.Get<IClientState>().LocalPlayer?.Name.ToString() ?? "YOU";

                if (sortedCombatants.Count > this.BarConfig.BarCount)
                {
                    currentIndex = Math.Clamp(_scrollPosition, 0, sortedCombatants.Count - this.BarConfig.BarCount);
                    _scrollPosition = currentIndex;

                    if (this.BarConfig.AlwaysShowSelf)
                    {
                        MovePlayerIntoViewableRange(sortedCombatants, _scrollPosition, playerName);
                    }
                }

                int maxIndex = Math.Min(currentIndex + this.BarConfig.BarCount, sortedCombatants.Count);
                for (; currentIndex < maxIndex; currentIndex++)
                {
                    Combatant combatant = sortedCombatants[currentIndex];
                    combatant.Rank = (currentIndex + 1).ToString();
                    UpdatePlayerName(combatant, playerName);

                    float current = this.GeneralConfig.DataType switch
                    {
                        MeterDataType.Damage => combatant.DamageTotal?.Value ?? 0,
                        MeterDataType.Healing => combatant.EffectiveHealing?.Value ?? 0,
                        MeterDataType.DamageTaken => combatant.DamageTaken?.Value ?? 0,
                        _ => 0
                    };

                    ConfigColor barColor = this.BarConfig.BarColor;
                    ConfigColor jobColor = this.BarColorsConfig.GetColor(combatant.Job);
                    localPos = this.BarConfig.DrawBar(drawList, localPos, size, combatant, jobColor, barColor, top, current);
                }
            }
        }

        private void MovePlayerIntoViewableRange(List<Combatant> sortedCombatants, int scrollPosition, string playerName)
        {
            int oldPlayerIndex = sortedCombatants.FindIndex(combatant => combatant.Name.Contains("YOU") || combatant.Name.Contains(playerName));
            if (oldPlayerIndex == -1)
            {
                return;
            }

            int newPlayerIndex = Math.Clamp(oldPlayerIndex, scrollPosition, this.BarConfig.BarCount + scrollPosition - 1);

            if (oldPlayerIndex == newPlayerIndex)
            {
                return;
            }

            sortedCombatants.MoveItem(oldPlayerIndex, newPlayerIndex);
        }

        private void UpdatePlayerName(Combatant combatant, string localPlayerName)
        {
            combatant.NameOverwrite = this.BarConfig.UseCharacterName switch
            {
                true when combatant.Name.Contains("YOU") => localPlayerName,
                false when combatant.NameOverwrite is not null => null,
                _ => combatant.NameOverwrite
            };
        }

        private bool DrawContextMenu(string popupId, out int selectedIndex)
        {
            selectedIndex = -1;
            bool selected = false;

            if (ImGui.BeginPopup(popupId))
            {
                if (!ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    ImGui.SetKeyboardFocusHere(0);
                }

                if (ImGui.Selectable("Current Data"))
                {
                    selected = true;
                }

                List<ActEvent> events = Singletons.Get<LogClient>().PastEvents;
                if (events.Count > 0)
                {
                    ImGui.Separator();
                }

                for (int i = events.Count - 1; i >= 0; i--)
                {
                    if (ImGui.Selectable($"{events[i].Encounter?.Duration}\t—\t{events[i].Encounter?.Title}"))
                    {
                        selectedIndex = i;
                        selected = true;
                    }
                }

                ImGui.Separator();
                if (ImGui.Selectable("Clear Data"))
                {
                    Singletons.Get<PluginManager>().Clear();
                    selected = true;
                }

                if (ImGui.Selectable("Configure"))
                {
                    Singletons.Get<PluginManager>().ConfigureMeter(this);
                    selected = true;
                }

                ImGui.EndPopup();
            }

            return selected;
        }

        private List<Combatant> GetSortedCombatants(ActEvent actEvent, MeterDataType dataType)
        {
            if (actEvent.Combatants is null ||
                _lastSortedTimestamp.HasValue &&
                _lastSortedTimestamp.Value == actEvent.Timestamp &&
                !this.GeneralConfig.Preview)
            {
                return _lastSortedCombatants;
            }

            List<Combatant> sortedCombatants = [.. actEvent.Combatants.Values];

            sortedCombatants.Sort((x, y) =>
            {
                float xFloat = dataType switch
                {
                    MeterDataType.Damage => x.DamageTotal?.Value ?? 0,
                    MeterDataType.Healing => x.EffectiveHealing?.Value ?? 0,
                    MeterDataType.DamageTaken => x.DamageTaken?.Value ?? 0,
                    _ => 0
                };

                float yFloat = dataType switch
                {
                    MeterDataType.Damage => y.DamageTotal?.Value ?? 0,
                    MeterDataType.Healing => y.EffectiveHealing?.Value ?? 0,
                    MeterDataType.DamageTaken => y.DamageTaken?.Value ?? 0,
                    _ => 0
                };

                return (int)(yFloat - xFloat);
            });

            _lastSortedTimestamp = actEvent.Timestamp;
            _lastSortedCombatants = sortedCombatants;
            return sortedCombatants;
        }
    }
}
