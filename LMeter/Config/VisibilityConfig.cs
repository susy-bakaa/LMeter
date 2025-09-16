﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using LMeter.Act;
using LMeter.Helpers;
using Newtonsoft.Json;

namespace LMeter.Config
{

    public class VisibilityConfig : IConfigPage
    {
        public string Name => "Visibility";

        public IConfigPage GetDefault() => new VisibilityConfig();

        [JsonIgnore] private string _customJobInput = string.Empty;

        public bool AlwaysHide = false;
        public bool HideInCombat = false;
        public bool HideOutsideCombat = false;
        public bool HideOutsideDuty = false;
        public bool HideWhilePerforming = false;
        public bool HideInPvP = false;
        public bool HideInGoldenSaucer = false;
        public bool HideInFieldOperations = false;
        public bool HideIfNotConnected = false;
        public bool ShouldClip = true;

        public JobType ShowForJobTypes = JobType.All;
        public string CustomJobString = string.Empty;
        public List<Job> CustomJobList = [];

        public bool IsVisible()
        {
            if (this.AlwaysHide)
            {
                return false;
            }

            if (this.HideInCombat && CharacterState.IsInCombat())
            {
                return false;
            }

            if (this.HideOutsideCombat && !CharacterState.IsInCombat())
            {
                return false;
            }

            if (this.HideOutsideDuty && !CharacterState.IsInDuty())
            {
                return false;
            }

            if (this.HideWhilePerforming && CharacterState.IsPerforming())
            {
                return false;
            }

            if (this.HideInPvP && CharacterState.IsInPvP())
            {
                return false;
            }

            if (this.HideInGoldenSaucer && CharacterState.IsInGoldenSaucer())
            {
                return false;
            }

            if (this.HideInFieldOperations && CharacterState.IsInFieldOperation())
            {
                return false;
            }

            if (this.HideIfNotConnected && Singletons.Get<LogClient>().Status != ConnectionStatus.Connected)
            {
                return false;
            }

            return CharacterState.IsJobType(CharacterState.GetCharacterJob(), this.ShowForJobTypes, this.CustomJobList);
        }

        public void DrawConfig(Vector2 size, float padX, float padY)
        {
            if (ImGui.BeginChild($"##{this.Name}", new Vector2(size.X, size.Y), true))
            {
                ImGui.Checkbox("Always Hide", ref this.AlwaysHide);
                ImGui.Checkbox("Hide In Combat", ref this.HideInCombat);
                ImGui.Checkbox("Hide Outside Combat", ref this.HideOutsideCombat);
                ImGui.Checkbox("Hide Outside Duty", ref this.HideOutsideDuty);
                ImGui.Checkbox("Hide While Performing", ref this.HideWhilePerforming);
                ImGui.Checkbox("Hide In PvP", ref this.HideInPvP);
                ImGui.Checkbox("Hide In Golden Saucer", ref this.HideInGoldenSaucer);
                ImGui.Checkbox("Hide In Field Operations", ref this.HideInFieldOperations);
                ImGui.Checkbox("Hide While Not Connected to ACT", ref this.HideIfNotConnected);
                ImGui.Checkbox("Hide When Covered by Game UI Window", ref this.ShouldClip);

                DrawHelpers.DrawSpacing(1);
                string[] jobTypeOptions = Enum.GetNames(typeof(JobType));
                ImGui.Combo("Show for Jobs", ref Unsafe.As<JobType, int>(ref this.ShowForJobTypes), jobTypeOptions, jobTypeOptions.Length);

                if (this.ShowForJobTypes == JobType.Custom)
                {
                    if (string.IsNullOrEmpty(_customJobInput))
                    {
                        _customJobInput = this.CustomJobString.ToUpper();
                    }

                    if (ImGui.InputTextWithHint("Custom Job List", "Comma Separated List (ex: WAR, SAM, BLM)", ref _customJobInput, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        IEnumerable<string> jobStrings = _customJobInput.Split(',').Select(j => j.Trim());
                        List<Job> jobList = [];
                        foreach (string j in jobStrings)
                        {
                            if (Enum.TryParse(j, true, out Job parsed))
                            {
                                jobList.Add(parsed);
                            }
                            else
                            {
                                jobList.Clear();
                                _customJobInput = string.Empty;
                                break;
                            }
                        }

                        _customJobInput = _customJobInput.ToUpper();
                        this.CustomJobString = _customJobInput;
                        this.CustomJobList = jobList;
                    }
                }
            }

            ImGui.EndChild();
        }
    }
}