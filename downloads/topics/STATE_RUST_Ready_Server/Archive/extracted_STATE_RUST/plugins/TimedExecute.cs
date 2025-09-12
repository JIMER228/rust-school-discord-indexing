using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Timed Execute", "PaiN/Nimant", "1.0.0", ResourceId = 0)]
    [Description("Execute commands every (x) seconds.")]
    class TimedExecute : RustPlugin
    {
		private TOD_Sky	Sky;
		private TOD_Time Time;
		
		private Dictionary<string, float>  timerRepeat 		= new Dictionary<string, float>();
		private Dictionary<string, float>  timerOnce 		= new Dictionary<string, float>();
		private Dictionary<string, string> realTimeTimer 	= new Dictionary<string, string>();
		private Dictionary<string, string> gameTimeTimer 	= new Dictionary<string, string>();
		
		private bool enableTimerRepeat = false;
		private bool enableTimerOnce = false;
		private bool enabledRealTimeTimer = false;
		private bool enabledGameTimeTimer = false;
		
		private bool showTimerInfo = false;
		
		private bool init = false;
		
        private Timer repeater;
        private Timer chaintimer;
        private Timer checkreal;
		private Timer checkgame;
		
		private string prevTime = "";

        private void Loaded()
        {			
			foreach(var item in Config["TimerRepeat"] as Dictionary<string, object>)
				timerRepeat.Add(item.Key, Convert.ToSingle(item.Value));
							
			foreach(var item in Config["TimerOnce"] as Dictionary<string, object>)
				timerOnce.Add(item.Key, Convert.ToSingle(item.Value));
			
			foreach(var item in Config["RealTime-Timer"] as Dictionary<string, object>)
				realTimeTimer.Add(item.Key, Convert.ToString(item.Value));
				
			foreach(var item in Config["GameTime-Timer"] as Dictionary<string, object>)
				gameTimeTimer.Add(item.Key, Convert.ToString(item.Value));													
			
			enableTimerRepeat = Convert.ToBoolean(Config["EnableTimerRepeat"]);
			enableTimerOnce = Convert.ToBoolean(Config["EnableTimerOnce"]);
			enabledRealTimeTimer = Convert.ToBoolean(Config["EnabledRealTime-Timer"]);
			enabledGameTimeTimer = Convert.ToBoolean(Config["EnabledGameTime-Timer"]);
			
			showTimerInfo = Convert.ToBoolean(Config["ShowTimerInfo"]);
			
            checkreal = timer.Repeat(1, 0, () => RealTime());			
            RunRepeater();
            RunOnce();

			if (showTimerInfo)
			{	
				Puts($"Timer-Once is {(Convert.ToBoolean(Config["EnableTimerOnce"]) == true ? "ON" : "OFF")}");
				Puts($"Timer-Repeat is {(Convert.ToBoolean(Config["EnableTimerRepeat"]) == true ? "ON" : "OFF")}");
				Puts($"Timer-RealTime is {(Convert.ToBoolean(Config["EnabledRealTime-Timer"]) == true ? "ON" : "OFF")}");
				Puts($"Timer-GameTime is {(Convert.ToBoolean(Config["EnabledGameTime-Timer"]) == true ? "ON" : "OFF")}");
			}
			
        }
		
		private void OnServerInitialized()
		{
			Sky = TOD_Sky.Instance;			
			Time = Sky.GetComponent<TOD_Components>().GetComponent<TOD_Time>();																					

			init = true;
		}

        private void RunRepeater()
        {
            if (repeater != null)
            {
                repeater.Destroy();
            }
            if (enableTimerRepeat == true)
            {
                foreach (var cmd in timerRepeat)
                {
                    repeater = timer.Repeat(cmd.Value, 0, () => {
                        if (SplitCommand(cmd.Key).Value.Length == 0)
                            rust.RunServerCommand(SplitCommand(cmd.Key).Key, null);
                        else
                            rust.RunServerCommand(SplitCommand(cmd.Key).Key, string.Join(" ", SplitCommand(cmd.Key).Value));

						if (showTimerInfo)
							Puts(string.Format("ran CMD: {0} || ARGS: {1}", SplitCommand(cmd.Key).Key, string.Join(" ", SplitCommand(cmd.Key).Value)));
                    });
                }
            }
        }

        private void RealTime()
        {
            if (enabledRealTimeTimer == true)
            {
				string curTime = System.DateTime.Now.ToString("HH:mm:ss");
				
                foreach (var cmd in realTimeTimer)
                {
                    if (curTime == cmd.Key)
                    {
                        if (SplitCommand(cmd.Value).Value.Length == 0)
                            rust.RunServerCommand(SplitCommand(cmd.Value).Key, null);
                        else
                            rust.RunServerCommand(SplitCommand(cmd.Value).Key, string.Join(" ", SplitCommand(cmd.Value).Value));

						if (showTimerInfo)
							Puts(string.Format("ran CMD: {0} || ARGS: {1}", SplitCommand(cmd.Value).Key, string.Join(" ", SplitCommand(cmd.Value).Value)));
                    }
                }
            }
        }
		
		private void GameTime()
        {
			if (!init) return;
			
            if (enabledGameTimeTimer == true)
            {
				string curTime = GetGameTime();
				
                foreach (var cmd in gameTimeTimer)				
                {							
                    if (curTime == cmd.Key && !prevTime.Equals(curTime))
                    {
                        if (SplitCommand(cmd.Value).Value.Length == 0)
                            rust.RunServerCommand(SplitCommand(cmd.Value).Key, null);
                        else
                            rust.RunServerCommand(SplitCommand(cmd.Value).Key, string.Join(" ", SplitCommand(cmd.Value).Value));

						if (showTimerInfo)
							Puts(string.Format("ran CMD: {0} || ARGS: {1}", SplitCommand(cmd.Value).Key, string.Join(" ", SplitCommand(cmd.Value).Value)));
                    }
                }								
				
				prevTime = curTime;
            }
        }
		
        private void RunOnce()
        {
            if (chaintimer != null)
            {
                chaintimer.Destroy();
            }
            if (enableTimerOnce == true)
            {
                foreach (var cmdc in timerOnce)
                {
                    chaintimer = timer.Once(cmdc.Value, () => {
                        if (SplitCommand(cmdc.Key).Value.Length == 0)
                            rust.RunServerCommand(SplitCommand(cmdc.Key).Key, null);
                        else
                            rust.RunServerCommand(SplitCommand(cmdc.Key).Key, string.Join(" ", SplitCommand(cmdc.Key).Value));

						if (showTimerInfo)
							Puts(string.Format("ran CMD: {0} || ARGS: {1}", SplitCommand(cmdc.Key).Key, string.Join(" ", SplitCommand(cmdc.Key).Value)));
                    });
                }
            }
        }
		
		private void OnTick() => GameTime();		

        private void Unload()
        {
            if (repeater != null)
            {
                repeater.Destroy();
				if (showTimerInfo)
					Puts("Destroyed the *Repeater* timer!");
            }
            if (chaintimer != null)
            {
                chaintimer.Destroy();
				if (showTimerInfo)
					Puts("Destroyed the *Timer-Once* timer!");
            }
            if (checkreal != null)
            {
                checkreal.Destroy();
				if (showTimerInfo)
					Puts("Destroyed the *RealTime* timer!");
            }
			if (checkgame != null)
            {
                checkgame.Destroy();
				if (showTimerInfo)
					Puts("Destroyed the *GameTime* timer!");
            }
        }

        private Dictionary<string, object> repeatcmds = new Dictionary<string, object>();
        private Dictionary<string, object> chaincmds = new Dictionary<string, object>();
        private Dictionary<string, object> realtimecmds = new Dictionary<string, object>();
		private Dictionary<string, object> gametimecmds = new Dictionary<string, object>();

        protected override void LoadDefaultConfig()
        {
            repeatcmds.Add("command1 arg", 300);
            repeatcmds.Add("command2 'msg'", 300);
            Puts("Creating a new configuration file!");

            if (Config["TimerRepeat"] == null) Config["TimerRepeat"] = repeatcmds;

            chaincmds.Add("command1 'msg'", 60);
            chaincmds.Add("command2 'msg'", 120);
            chaincmds.Add("command3 arg", 180);
            chaincmds.Add("command4 arg", 181);
            if (Config["TimerOnce"] == null) Config["TimerOnce"] = chaincmds;

            if (Config["EnableTimerRepeat"] == null) Config["EnableTimerRepeat"] = true;
            if (Config["EnableTimerOnce"] == null) Config["EnableTimerOnce"] = true;
            if (Config["EnabledRealTime-Timer"] == null) Config["EnabledRealTime-Timer"] = true;
			if (Config["EnabledGameTime-Timer"] == null) Config["EnabledGameTime-Timer"] = true;

            realtimecmds.Add("16:00:00", "command1 arg");
            realtimecmds.Add("16:30:00", "command2 arg");
            realtimecmds.Add("17:00:00", "command3 arg");
            realtimecmds.Add("18:00:00", "command4 arg");
            if (Config["RealTime-Timer"] == null) Config["RealTime-Timer"] = realtimecmds;
			
			gametimecmds.Add("06:00:00", "command1 arg");
            gametimecmds.Add("18:00:00", "command2 arg");
            if (Config["GameTime-Timer"] == null) Config["GameTime-Timer"] = gametimecmds;
			
			if (Config["ShowTimerInfo"] == null) Config["ShowTimerInfo"] = false;
        }        
		
		[ConsoleCommand("resetoncetimer")]
        private void cmdResOnceTimer(ConsoleSystem.Arg arg)
		{
            if (arg == null) return;
			if (arg.Player() != null) return;			
			
			RunOnce();	
			Puts("Once timer was reset.");	
		}					        
		
		private string GetGameTime()
		{
			int hours = (int)Math.Truncate(Sky.Cycle.Hour);
			int minutes = (int)Math.Round((Sky.Cycle.Hour - (float)hours)*60f);
			
			return ((hours<10)?"0"+hours.ToString():hours.ToString())+":"+
				   ((minutes<10)?"0"+minutes.ToString():minutes.ToString())+":00";
		}

        private KeyValuePair<string, string[]> SplitCommand(string cmd)
        {
            string[] CmdSplit = cmd.Split(' ');
            string command = CmdSplit[0];
            var args = CmdSplit.Skip(1);

            return new KeyValuePair<string, string[]>(command, args.ToArray());
        }
        
    }
}