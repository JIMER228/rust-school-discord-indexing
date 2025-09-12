using Oxide.Core; 
using Oxide.Game.Rust; 

namespace Oxide.Plugins 
{ 
[Info("FixAC", "Аслан", "1.0")] 
public class LengthFix : RustPlugin 
{ 
void OnPlayerInit(BasePlayer player) 
{ 
if (player.userID.ToString().Length != 17) 
player.Kick("[Jurpl Rust] Kick AntiCheat"); 
} 
} 
}