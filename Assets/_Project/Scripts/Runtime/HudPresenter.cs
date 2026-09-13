using UnityEngine;

namespace DropletPrototype
{
    public sealed class HudPresenter : MonoBehaviour
    {
        public MissionController mission;
        public Camera viewCamera;
        public PlayerOptions options;
        public MissionEffects effects;
        public string locationLabel = "EARTH ORBIT  /  TRAINING SORTIE";
        GUIStyle title, body, small, button, value, centered, eyebrow, targetLabel, targetSymbol;
        bool settingsOpen;
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
                if (GUI.Button(new Rect(910,621,155,42),"PAUSE [ESC]",button)) mission.TogglePause();
                if (GUI.Button(new Rect(1080,621,170,42),"SKIP [TAB]",button)) mission.narrative.Skip();
            }
            else if(settingsOpen&&options!=null)DrawSettings();else DrawMenu();
            GUI.matrix=old;
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
                if(GUI.Button(new Rect(82,501,434,50),mission.narrative != null ? "BEGIN APPROACH   [ ENTER ]" : "BEGIN SORTIE   [ ENTER ]",button))mission.StartMission();
                if (mission.narrative != null && GUI.Button(new Rect(590,591,355,44),"DIRECT COMBAT [R]",button)) mission.RestartIntoCombat();
            }
            else if(mission.State==MissionState.Paused)
            {
                GUI.Label(new Rect(82,293,415,75),"Time and flight are suspended.\nChoose your next approach, or adjust the controls.",body);
                if(GUI.Button(new Rect(82,411,434,50),"RESUME   [ ESC ]",button))mission.TogglePause();
                if(GUI.Button(new Rect(82,478,434,50),"RESTART   [ R ]",button)){if(mission.combat != null)mission.RestartIntoCombat();else mission.Restart();}
            }
            else
            {
                GUI.Label(new Rect(82,289,415,111),$"{mission.score.Score:00000}  POINTS\n{mission.DestroyedCount} / {mission.TotalCount}  SHIPS CLEARED\n{mission.Elapsed:0.00}s  ELAPSED",value);
                GUI.Label(new Rect(82,416,415,64),mission.combat != null ? $"RATING {mission.Rating}   /   {mission.EscapedCount} ESCAPED\nEscaped ships earn no kill or time bonus." : mission.Won?"Completion bonus included.\nFind a cleaner line. Leave nothing behind.":"Brake into turns. Follow the amber target cue.\nKeep your next approach in view.",body);
                if(GUI.Button(new Rect(82,501,434,50),"FLY AGAIN   [ R ]",button)){if(mission.combat != null)mission.RestartIntoCombat();else mission.Restart();}
                if (mission.narrative != null && GUI.Button(new Rect(590,591,355,44),"REPLAY APPROACH [N]",button)) mission.ReplayNarrative();
            }
            if(options!=null&&GUI.Button(new Rect(82,572,270,43),"SETTINGS",button))settingsOpen=true;
            if(GUI.Button(new Rect(368,572,148,43),"QUIT",button)) {if(!Application.isEditor)Application.Quit();}
            GUI.Label(new Rect(590,649,640,35),"METAL / MOMENTUM / SILENCE",eyebrow);
        }
        void DrawSettings()
        {
            Panel(new Rect(270,141,740,520));GUI.Label(new Rect(302,163,660,60),"FLIGHT SETTINGS",title);
            float sens=Slider(244,"MOUSE SENSITIVITY",options.Sensitivity,.35f,2.5f,"x");
            float fov=Slider(319,"FIELD OF VIEW",options.FieldOfView,50,90,"deg");
            float volume=Slider(394,"MASTER VOLUME",options.Volume,0,1,"");
            bool invert=GUI.Toggle(new Rect(305,467,310,35),options.InvertY,(options.InvertY?"[ON] ":"[OFF] ")+"Invert Y",body);
            bool reduced=GUI.Toggle(new Rect(625,467,330,35),options.ReducedMotion,(options.ReducedMotion?"[ON] ":"[OFF] ")+"Reduce camera motion",body);
            if(sens!=options.Sensitivity||fov!=options.FieldOfView||volume!=options.Volume||invert!=options.InvertY||reduced!=options.ReducedMotion)options.Apply(sens,invert,fov,volume,reduced);
            GUI.Label(new Rect(305,512,210,34),"EFFECTS BUDGET",small);
            if(effects!=null)
            {
                int choice=GUI.SelectionGrid(new Rect(515,507,440,36),(int)effects.Quality,new[]{effects.Quality==EffectQuality.Off?"[ OFF ]":"OFF",effects.Quality==EffectQuality.Low?"[ LOW ]":"LOW",effects.Quality==EffectQuality.High?"[ HIGH ]":"HIGH"},3,button);
                if(choice!=(int)effects.Quality)effects.Quality=(EffectQuality)choice;
            }
            if(GUI.Button(new Rect(305,586,315,45),"RESET DEFAULTS",button))
            {options.Apply(1,false,65,.65f,true);if(effects!=null)effects.Quality=EffectQuality.High;PlayerPrefs.Save();}
            if(GUI.Button(new Rect(640,586,315,45),"BACK",button)){settingsOpen=false;PlayerPrefs.Save();}
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
