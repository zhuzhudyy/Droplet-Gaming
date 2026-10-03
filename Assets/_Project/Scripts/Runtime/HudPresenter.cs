using UnityEngine;

namespace DropletPrototype
{
    public sealed class HudPresenter : MonoBehaviour
    {
        public MissionController mission;
        public Camera viewCamera;
        public PlayerOptions options;
        public MissionEffects effects;
        public UiMissionAudioController audioFeedback;
        public SeedAudioMixController audioMix;
        public string locationLabel = "EARTH ORBIT  /  TRAINING SORTIE";
        GUIStyle title, body, small, button, value, centered, eyebrow, targetLabel, targetSymbol;
        bool settingsOpen;
        string focusedAudioControl;
        static readonly Color Cyan = new Color(.38f,.8f,.93f);
        static readonly Color Gold = new Color(1,.62f,.28f);
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=39,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            body=new GUIStyle(GUI.skin.label){fontSize=19,wordWrap=true,normal={textColor=new Color(.72f,.8f,.88f)}};
            small=new GUIStyle(body){fontSize=14};
            eyebrow=new GUIStyle(small){fontStyle=FontStyle.Bold,normal={textColor=Cyan}};
            value=new GUIStyle(title){fontSize=25};centered=new GUIStyle(body){alignment=TextAnchor.MiddleCenter};
            targetLabel=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,normal={textColor=Gold}};
            targetSymbol=new GUIStyle(centered){fontStyle=FontStyle.Bold,normal={textColor=Gold}};
            button=new GUIStyle(GUI.skin.button){fontSize=18,fontStyle=FontStyle.Bold};
        }
        void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Panel(Rect r){Fill(r,new Color(.012f,.026f,.045f,.93f));Fill(new Rect(r.x,r.y,2,r.height),Cyan*.8f);}
        void OnGUI()
        {
            if(mission==null||mission.score==null)return;Styles();
            var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Panel(new Rect(24,20,1232,76));
            GUI.Label(new Rect(45,29,265,22),"D R O P L E T   /   "+(mission.TotalCount>10?"FLEET ASSAULT":"TEST RANGE"),eyebrow);
            GUI.Label(new Rect(45,53,265,32),mission.State.ToString().ToUpperInvariant(),value);
            Stat(355,"REMAINING",$"{Mathf.CeilToInt(mission.Remaining)/60:00}:{Mathf.CeilToInt(mission.Remaining)%60:00}");
            Stat(560,"FLEET",$"{mission.DestroyedCount:00} / {mission.TotalCount:00}");
            Stat(770,"SCORE",$"{mission.score.Score:00000}");
            Stat(960,"CHAIN",$"x{mission.score.Multiplier}");
            Stat(1110,mission.combat != null ? "km/s" : "VELOCITY",mission.combat != null ? $"{mission.settings.ToMeters(mission.motor.MeasuredSpeed)/1000f:0.0}" : $"{mission.motor.Speed:0}");
            if (mission.combat != null)
                GUI.Label(new Rect(40,100,880,26),$"INTACT {mission.IntactCount}     REACTOR CRITICAL {mission.PendingCount}     EXPLODED {mission.DestroyedCount}     ESCAPED {mission.EscapedCount}",eyebrow);
            if(mission.State==MissionState.Playing)
            {
                settingsOpen=false;Crosshair();DrawTargetGuide();
                Fill(new Rect(961,86,110*Mathf.Clamp01(mission.score.ComboRemaining/mission.settings.comboWindow),2),Gold);
                if(mission.NearBoundary||mission.RecoveryNotice>0){GUI.color=Gold;GUI.Label(new Rect(235,120,810,35),mission.RecoveryNotice>0?"SAFE RETURN  /  Fleet and score preserved":"ARENA LIMIT  /  Turn back toward the fleet",centered);GUI.color=Color.white;}
                GUI.Label(new Rect(30,673,1220,28),"MOUSE  steer     W / S  speed     A / D  strafe     SHIFT  boost     SPACE  brake     ESC  pause",small);
            }
            else if(mission.State==MissionState.Narrative)
            {
                GUI.Label(new Rect(35,674,850,26),"INTERCEPTED APPROACH   /   TAB skip    ESC pause    N replay    R combat",small);
                if (AudioButton(new Rect(910,621,155,42),"PAUSE [ESC]","NarrativePause")) mission.TogglePause();
                if (AudioButton(new Rect(1080,621,170,42),"SKIP [TAB]","NarrativeSkip")) mission.narrative.Skip();
            }
            else if(settingsOpen&&options!=null)DrawSettings();else DrawMenu();
            if (Event.current.type == EventType.Repaint)
            {
                string focused = GUI.GetNameOfFocusedControl();
                if (focused != focusedAudioControl)
                {
                    focusedAudioControl = focused;
                    if (!string.IsNullOrEmpty(focused) && focused.StartsWith("SeedAudio_"))
                        audioFeedback?.NotifyUi(SeedUiAction.Focus);
                }
            }
            GUI.matrix=old;
        }
        bool AudioButton(Rect rect, string label, string id)
        {
            GUI.SetNextControlName("SeedAudio_" + id);
            return GUI.Button(rect, label, button);
        }
        void Stat(float x,string label,string text){GUI.Label(new Rect(x,29,180,22),label,small);GUI.Label(new Rect(x,53,180,32),text,value);}
        void Crosshair()
        {
            Fill(new Rect(625,359,10,1),Cyan);Fill(new Rect(645,359,10,1),Cyan);
            Fill(new Rect(639,345,1,10),Cyan);Fill(new Rect(639,365,1,10),Cyan);
        }
        void DrawMenu()
        {
            Panel(new Rect(55,166,490,485));
            GUI.Label(new Rect(80,188,440,25),locationLabel,eyebrow);
            string heading=mission.State==MissionState.Ready?(mission.TotalCount>10?"FLEET ASSAULT":"TEST RANGE"):mission.State==MissionState.Paused?"FLIGHT PAUSED":mission.Won?"FLEET SILENCED":mission.EscapedCount>0?"FLEET DISPERSED":"TIME EXPIRED";
            GUI.Label(new Rect(78,223,445,58),heading,title);
            if(mission.State==MissionState.Ready)
            {
                GUI.Label(new Rect(82,287,415,80),$"One indestructible droplet. {mission.TotalCount} targets.\nClear the fleet within {mission.settings.missionSeconds:0} seconds.\nFly straight through the hulls.",body);
                GUI.Label(new Rect(82,383,415,94),$"Mouse steers. W / S sets cruise speed.\nShift boosts. Space brakes for turns.\nA / D adjusts your line. Esc pauses.\nChain impacts within {mission.settings.comboWindow:0} seconds for bonus score.",small);
                if(AudioButton(new Rect(82,501,434,50),mission.narrative != null ? "BEGIN APPROACH   [ ENTER ]" : "BEGIN SORTIE   [ ENTER ]","Begin"))
                { audioFeedback?.NotifyUi(SeedUiAction.Confirm); mission.StartMission(); }
                if (mission.narrative != null && AudioButton(new Rect(590,591,355,44),"DIRECT COMBAT [R]","DirectCombat")) mission.RestartIntoCombat();
            }
            else if(mission.State==MissionState.Paused)
            {
                GUI.Label(new Rect(82,293,415,75),"Time and flight are suspended.\nChoose your next approach, or adjust the controls.",body);
                if(AudioButton(new Rect(82,411,434,50),"RESUME   [ ESC ]","Resume"))mission.TogglePause();
                if(AudioButton(new Rect(82,478,434,50),"RESTART   [ R ]","Restart")){if(mission.combat != null)mission.RestartIntoCombat();else mission.Restart();}
            }
            else
            {
                GUI.Label(new Rect(82,289,415,111),$"{mission.score.Score:00000}  POINTS\n{mission.DestroyedCount} / {mission.TotalCount}  SHIPS CLEARED\n{mission.Elapsed:0.00}s  ELAPSED",value);
                GUI.Label(new Rect(82,416,415,64),mission.combat != null ? $"RATING {mission.Rating}   /   {mission.EscapedCount} ESCAPED\nEscaped ships earn no kill or time bonus." : mission.Won?"Completion bonus included.\nFind a cleaner line. Leave nothing behind.":"Brake into turns. Follow the amber target cue.\nKeep your next approach in view.",body);
                if(AudioButton(new Rect(82,501,434,50),"FLY AGAIN   [ R ]","FlyAgain")){if(mission.combat != null)mission.RestartIntoCombat();else mission.Restart();}
                if (mission.narrative != null && AudioButton(new Rect(590,591,355,44),"REPLAY APPROACH [N]","Replay")) mission.ReplayNarrative();
            }
            if(options!=null&&AudioButton(new Rect(82,572,270,43),"SETTINGS","Settings"))
            { settingsOpen=true; audioFeedback?.NotifyUi(SeedUiAction.Confirm); }
            if(AudioButton(new Rect(368,572,148,43),"QUIT","Quit"))
            { audioFeedback?.NotifyUi(SeedUiAction.Confirm); if(!Application.isEditor)Application.Quit(); }
            GUI.Label(new Rect(590,649,640,35),"METAL / MOMENTUM / SILENCE",eyebrow);
        }
        void DrawSettings()
        {
            if (audioMix != null) { DrawSeedAudioSettings(); return; }
            Panel(new Rect(270,141,740,520));GUI.Label(new Rect(302,163,660,60),"FLIGHT SETTINGS",title);
            float sens=Slider(244,"MOUSE SENSITIVITY",options.Sensitivity,.35f,2.5f,"x");
            float fov=Slider(319,"FIELD OF VIEW",options.FieldOfView,50,90,"deg");
            float volume=Slider(394,"MASTER VOLUME",options.Volume,0,1,"");
            bool invert=GUI.Toggle(new Rect(305,467,310,35),options.InvertY,(options.InvertY?"[ON] ":"[OFF] ")+"Invert Y",body);
            bool reduced=GUI.Toggle(new Rect(625,467,330,35),options.ReducedMotion,(options.ReducedMotion?"[ON] ":"[OFF] ")+"Reduce camera motion",body);
            bool sliderMoved = Mathf.RoundToInt(sens*20f)!=Mathf.RoundToInt(options.Sensitivity*20f) ||
                               Mathf.RoundToInt(fov)!=Mathf.RoundToInt(options.FieldOfView) ||
                               Mathf.RoundToInt(volume*20f)!=Mathf.RoundToInt(options.Volume*20f);
            bool toggleChanged = invert!=options.InvertY || reduced!=options.ReducedMotion;
            if(sens!=options.Sensitivity||fov!=options.FieldOfView||volume!=options.Volume||toggleChanged)
            {
                options.Apply(sens,invert,fov,volume,reduced);
                if (sliderMoved) audioFeedback?.NotifyUi(SeedUiAction.Slider);
                else if (toggleChanged) audioFeedback?.NotifyUi(SeedUiAction.Toggle);
            }
            GUI.Label(new Rect(305,512,210,34),"EFFECTS BUDGET",small);
            if(effects!=null)
            {
                int choice=GUI.SelectionGrid(new Rect(515,507,440,36),(int)effects.Quality,new[]{effects.Quality==EffectQuality.Off?"[ OFF ]":"OFF",effects.Quality==EffectQuality.Low?"[ LOW ]":"LOW",effects.Quality==EffectQuality.High?"[ HIGH ]":"HIGH"},3,button);
                if(choice!=(int)effects.Quality)
                { effects.Quality=(EffectQuality)choice; audioFeedback?.NotifyUi(SeedUiAction.Toggle); }
            }
            if(AudioButton(new Rect(305,586,315,45),"RESET DEFAULTS","Defaults"))
            {options.Apply(1,false,65,.65f,true);if(effects!=null)effects.Quality=EffectQuality.High;PlayerPrefs.Save();audioFeedback?.NotifyUi(SeedUiAction.Confirm);}
            if(AudioButton(new Rect(640,586,315,45),"BACK","SettingsBack"))
            {settingsOpen=false;PlayerPrefs.Save();audioFeedback?.NotifyUi(SeedUiAction.Back);}
        }
        void DrawSeedAudioSettings()
        {
            Panel(new Rect(70,108,1140,560));
            GUI.Label(new Rect(100,123,1050,54),"FLIGHT  /  AUDIO SETTINGS",title);
            const float left = 102f, right = 665f, width = 505f;
            float sens = CompactSlider(left,183,width,"MOUSE SENSITIVITY",options.Sensitivity,.35f,2.5f,"x");
            float fov = CompactSlider(left,250,width,"FIELD OF VIEW",options.FieldOfView,50f,90f,"deg");
            float master = CompactSlider(left,317,width,"MASTER VOLUME",options.Volume,0f,1f,"");
            bool invert = GUI.Toggle(new Rect(left,397,245,32),options.InvertY,
                (options.InvertY?"[ON] ":"[OFF] ")+"Invert Y",body);
            bool reduced = GUI.Toggle(new Rect(left+260,397,255,32),options.ReducedMotion,
                (options.ReducedMotion?"[ON] ":"[OFF] ")+"Reduce camera motion",body);
            bool sliderMoved = Mathf.RoundToInt(sens*20f)!=Mathf.RoundToInt(options.Sensitivity*20f) ||
                               Mathf.RoundToInt(fov)!=Mathf.RoundToInt(options.FieldOfView) ||
                               Mathf.RoundToInt(master*20f)!=Mathf.RoundToInt(options.Volume*20f);
            bool toggleChanged = invert!=options.InvertY || reduced!=options.ReducedMotion;
            if (sens!=options.Sensitivity || fov!=options.FieldOfView || master!=options.Volume || toggleChanged)
            {
                options.Apply(sens,invert,fov,master,reduced);
                if (sliderMoved) audioFeedback?.NotifyUi(SeedUiAction.Slider);
                else if (toggleChanged) audioFeedback?.NotifyUi(SeedUiAction.Toggle);
            }
            GUI.Label(new Rect(left,456,170,27),"EFFECTS BUDGET",small);
            if (effects != null)
            {
                int choice = GUI.SelectionGrid(new Rect(left+180,450,325,35),(int)effects.Quality,
                    new[]{"OFF","LOW","HIGH"},3,button);
                if (choice != (int)effects.Quality)
                { effects.Quality = (EffectQuality)choice; audioFeedback?.NotifyUi(SeedUiAction.Toggle); }
            }
            GUI.Label(new Rect(right,160,width,25),"SOUND GROUPS",eyebrow);
            GroupSlider(right,188,width,"MUSIC",SeedAudioBus.Music);
            GroupSlider(right,247,width,"AMBIENCE",SeedAudioBus.Ambience);
            GroupSlider(right,306,width,"FLIGHT",SeedAudioBus.Flight);
            GroupSlider(right,365,width,"COMBAT",SeedAudioBus.Combat);
            GroupSlider(right,424,width,"COMMUNICATIONS",SeedAudioBus.Communications);
            GroupSlider(right,483,width,"INTERFACE",SeedAudioBus.Ui);
            GroupSlider(right,542,width,"MISSION FEEDBACK",SeedAudioBus.Mission);
            if (AudioButton(new Rect(left,585,240,46),"RESET DEFAULTS","Defaults"))
            {
                options.Apply(1,false,65,.65f,true);
                audioMix.ResetUserVolumes();
                if (effects != null) effects.Quality = EffectQuality.High;
                PlayerPrefs.Save();
                audioFeedback?.NotifyUi(SeedUiAction.Confirm);
            }
            if (AudioButton(new Rect(left+261,585,240,46),"BACK","SettingsBack"))
            { settingsOpen=false; PlayerPrefs.Save(); audioFeedback?.NotifyUi(SeedUiAction.Back); }
        }
        float CompactSlider(float x,float y,float width,string label,float value,float min,float max,string suffix)
        {
            GUI.Label(new Rect(x,y,width-110,25),label,small);
            GUI.Label(new Rect(x+width-105,y,105,25),$"{value:0.00} {suffix}",small);
            return GUI.HorizontalSlider(new Rect(x,y+30,width,20),value,min,max);
        }
        void GroupSlider(float x,float y,float width,string label,SeedAudioBus bus)
        {
            float before = audioMix.GetUserVolume(bus);
            GUI.Label(new Rect(x,y,width-80,23),label,small);
            GUI.Label(new Rect(x+width-75,y,75,23),$"{Mathf.RoundToInt(before*100f)}%",small);
            float after = GUI.HorizontalSlider(new Rect(x,y+26,width,18),before,0f,1f);
            if (Mathf.Abs(after-before) <= .0001f) return;
            audioMix.SetUserVolume(bus,after);
            if (Mathf.RoundToInt(after*20f) != Mathf.RoundToInt(before*20f))
                audioFeedback?.NotifyUi(SeedUiAction.Slider);
        }
        float Slider(float y,string label,float v,float min,float max,string suffix)
        {GUI.Label(new Rect(305,y,490,28),label,small);GUI.Label(new Rect(821,y,160,28),$"{v:0.00} {suffix}",small);return GUI.HorizontalSlider(new Rect(306,y+37,648,22),v,min,max);}
        void DrawTargetGuide()
        {
            ShipTarget nearest=null;float best=float.PositiveInfinity;
            foreach(var target in mission.targets){if(target==null||target.IsResolved||target.DamageState==ShipDamageState.FatalPending)continue;float d=(mission.motor.transform.position-target.transform.position).sqrMagnitude;if(d<best){best=d;nearest=target;}}
            if(nearest==null||viewCamera==null)return;best=Mathf.Sqrt(best);
            Vector3 point=viewCamera.WorldToViewportPoint(nearest.transform.position);
            bool on=point.z>0&&point.x>.08f&&point.x<.92f&&point.y>.15f&&point.y<.80f;
            Vector2 p;
            if(on)p=new Vector2(point.x*1280,(1-point.y)*720);
            else{Vector3 local=viewCamera.transform.InverseTransformPoint(nearest.transform.position);Vector2 d=new Vector2(local.x,-local.y);if(d.sqrMagnitude<.001f)d=Vector2.right;d.Normalize();float f=Mathf.Min(530/Mathf.Max(.001f,Mathf.Abs(d.x)),220/Mathf.Max(.001f,Mathf.Abs(d.y)));p=new Vector2(640,385)+d*f;}
            Fill(new Rect(p.x-24,p.y-18,48,34),new Color(.006f,.012f,.02f,.9f));
            GUI.Label(new Rect(p.x-22,p.y-19,44,38),on?"[ + ]":">>",targetSymbol);
            string kind=nearest.targetId.Contains("Command")?"COMMAND":nearest.targetId.Contains("Large")?"CRUISER":"TARGET";
            Fill(new Rect(p.x-116,p.y+18,232,30),new Color(.006f,.012f,.02f,.94f));
            string distance = mission.combat != null ? $"{mission.settings.ToMeters(best)/1000f:0.0} km" : $"{best:0} m";
            GUI.Label(new Rect(p.x-110,p.y+18,220,30),$"{kind}  {distance}"+(point.z<0?"  / BEHIND":""),targetLabel);
        }
    }
}
