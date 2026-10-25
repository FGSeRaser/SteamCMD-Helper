''' <summary>A dedicated server that SteamCMD can install.</summary>
Friend Class GameServer
    Public Property Name As String = ""
    Public Property AppId As Integer
    Public Property Category As String = ""
    ''' <summary>True when the server files are only available with a Steam account that owns the game.</summary>
    Public Property NeedsLogin As Boolean
    ''' <summary>Program relative to the install folder, used by "Server starten".</summary>
    Public Property StartExe As String = ""
    Public Property StartArgs As String = ""
    Public Property Note As String = ""
    Public Property Beta As String = ""
    Public Property IsCustom As Boolean
    ''' <summary>Comes from the online catalog (catalog.json on GitHub), not from the program itself.</summary>
    Public Property FromOnline As Boolean

    ''' <summary>Folder name below the install root.</summary>
    Public ReadOnly Property FolderName As String
        Get
            Dim invalid = IO.Path.GetInvalidFileNameChars()
            Dim cleaned = New String(Name.Where(Function(c) Not invalid.Contains(c) AndAlso c <> "'"c).ToArray())
            Return cleaned.Replace(" ", "").Replace(":", "").Replace("&", "und")
        End Get
    End Property

    Public Function Clone() As GameServer
        Return DirectCast(MemberwiseClone(), GameServer)
    End Function

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

''' <summary>
''' Built-in server list. App IDs and login requirements follow the LinuxGSM configs
''' (github.com/GameServerManagers/LinuxGSM) and the Pelican/Pterodactyl eggs where available.
''' More servers come from the online catalog (catalog.json on GitHub) and from any App-ID.
''' </summary>
Friend NotInheritable Class Catalog

    Private Sub New()
    End Sub

    Public Const Shooter As String = "Shooter"
    Public Const Survival As String = "Survival"
    Public Const Sandbox As String = "Aufbau & Sandbox"
    Public Const Source As String = "Source & GoldSrc"
    Public Const Racing As String = "Rennen & Simulation"
    Public Const Other As String = "Sonstige"

    Public Shared ReadOnly Categories As String() = {Shooter, Survival, Sandbox, Source, Racing, Other}

    Private Shared Function G(name As String, appId As Integer, category As String,
                              Optional login As Boolean = False, Optional exe As String = "",
                              Optional args As String = "", Optional note As String = "") As GameServer
        Return New GameServer With {.Name = name, .AppId = appId, .Category = category, .NeedsLogin = login,
                                    .StartExe = exe, .StartArgs = args, .Note = note}
    End Function

    Public Shared Function BuiltIn() As List(Of GameServer)
        Const Srcds As String = "srcds.exe"
        Return New List(Of GameServer) From {
            G("Counter-Strike 2", 730, Shooter, exe:="game\bin\win64\cs2.exe", args:="-dedicated -console +map de_dust2 +maxplayers 10",
              note:="Für öffentliche Server brauchst du ein Game Server Login Token (+sv_setsteamaccount)."),
            G("Counter-Strike: Global Offensive (Legacy)", 740, Shooter, exe:=Srcds, args:="-console -game csgo +map de_dust2"),
            G("Counter-Strike: Source", 232330, Source, exe:=Srcds, args:="-console -game cstrike +map de_dust2 +maxplayers 16"),
            G("Counter-Strike 1.6", 90, Source, exe:="hlds.exe", args:="-console -game cstrike +map de_dust2 +maxplayers 16"),
            G("Team Fortress 2", 232250, Source, exe:=Srcds, args:="-console -game tf +map ctf_2fort +maxplayers 24"),
            G("Team Fortress 2 Classified", 3557020, Source),
            G("Garry's Mod", 4020, Source, exe:=Srcds, args:="-console -game garrysmod +gamemode sandbox +map gm_construct +maxplayers 16"),
            G("Day of Defeat: Source", 232290, Source, exe:=Srcds, args:="-console -game dod +map dod_anzio +maxplayers 24"),
            G("Half-Life 2: Deathmatch", 232370, Source, exe:=Srcds, args:="-console -game hl2mp +map dm_lockdown +maxplayers 16"),
            G("Half-Life Deathmatch: Source", 255470, Source, exe:=Srcds, args:="-console -game hl1mp +map crossfire +maxplayers 16"),
            G("Left 4 Dead", 222840, Source, exe:=Srcds, args:="-console -game left4dead +map l4d_hospital01_apartment"),
            G("Left 4 Dead 2", 222860, Source, exe:=Srcds, args:="-console -game left4dead2 +map c1m1_hotel"),
            G("Black Mesa: Deathmatch", 346680, Source, exe:=Srcds, args:="-console -game bms"),
            G("Zombie Panic! Source", 17505, Source, exe:=Srcds, args:="-console -game zps"),
            G("No More Room in Hell", 317670, Source, exe:=Srcds, args:="-console -game nmrih +map nmo_broadway"),
            G("Fistful of Frags", 295230, Source, exe:=Srcds, args:="-console -game fof"),
            G("Insurgency (2014)", 237410, Source, exe:=Srcds, args:="-console -game insurgency"),
            G("Day of Infamy", 462310, Source, exe:=Srcds, args:="-console -game doi"),
            G("Dystopia", 17585, Source, exe:=Srcds, args:="-console -game dystopia"),
            G("Pirates, Vikings & Knights II", 17575, Source, exe:=Srcds, args:="-console -game pvkii"),
            G("Nuclear Dawn", 111710, Source, login:=True, exe:=Srcds, args:="-console -game nucleardawn"),
            G("Codename CURE", 383410, Source, exe:=Srcds, args:="-console -game cure"),
            G("Synergy", 17525, Source, exe:=Srcds, args:="-console -game synergy"),
            G("Sven Co-op", 276060, Source),
            G("Natural Selection 2", 4940, Source),
            G("NS2: Combat", 313900, Source),
            G("Empires Mod", 460040, Source, exe:=Srcds, args:="-console -game empires"),
            G("Double Action: Boogaloo", 317800, Source, exe:=Srcds, args:="-console -game dab"),
            G("Blade Symphony", 228780, Source, login:=True),
            G("Contagion", 238430, Source, login:=True),
            G("BrainBread 2", 475370, Source),
            G("Military Conflict: Vietnam", 1136190, Source),
            G("Action: Source", 985050, Source, login:=True),
            G("IOSoccer", 673990, Source),
            G("Jabroni Brawl: Episode 3", 869800, Source),
            G("SourceForts Classic", 244310, Source),
            G("Insurgency: Sandstorm", 581330, Shooter),
            G("Squad", 403240, Shooter, exe:="SquadGameServer.exe", args:="Port=7787 QueryPort=27165 FIXEDMAXPLAYERS=80 RANDOM=NONE -log"),
            G("Squad 44 (Post Scriptum)", 746200, Shooter),
            G("Arma 3", 233780, Shooter, login:=True, exe:="arma3server_x64.exe", args:="-port=2302 -config=server.cfg -profiles=profiles -name=server"),
            G("Arma Reforger", 1874900, Shooter, exe:="ArmaReforgerServer.exe", args:="-config config.json -maxFPS 60"),
            G("DayZ", 223350, Shooter, login:=True, exe:="DayZServer_x64.exe", args:="-config=serverDZ.cfg -port=2302 -profiles=profiles -dologs -adminlog"),
            G("Killing Floor 2", 232130, Shooter, exe:="Binaries\Win64\KFServer.exe", args:="KF-BioticsLab"),
            G("Killing Floor", 215360, Shooter, login:=True),
            G("MORDHAU", 629800, Shooter, exe:="MordhauServer.exe", args:="-log"),
            G("Chivalry: Medieval Warfare", 220070, Shooter),
            G("Pavlov VR", 622970, Shooter),
            G("Ground Branch", 476400, Shooter),
            G("Holdfast: Nations At War", 1424230, Shooter),
            G("Operation: Harsh Doorstop", 950900, Shooter),
            G("BATTALION: Legacy", 805140, Shooter),
            G("Ballistic Overkill", 416880, Shooter),
            G("SCP: Secret Laboratory", 996560, Shooter, exe:="LocalAdmin.exe", args:="7777"),
            G("Quake Live", 349090, Shooter),
            G("Warfork", 1136510, Shooter),
            G("Soldat", 638500, Shooter),
            G("Running With Rifles", 270150, Shooter, login:=True),
            G("Red Orchestra: Ostfront 41-45", 223250, Shooter, login:=True),
            G("Red Orchestra 2", 212542, Shooter, login:=True),
            G("Homefront", 55280, Shooter, login:=True),
            G("Aliens vs Predator", 34120, Shooter, login:=True),
            G("Call of Duty: Modern Warfare 3", 42750, Shooter, login:=True),
            G("Sniper Elite V2", 208050, Shooter, login:=True),
            G("Sniper Elite 3", 266910, Shooter, login:=True),
            G("Serious Sam HD", 41005, Shooter, login:=True),
            G("Serious Sam 3", 41080, Shooter, login:=True),
            G("The Haunted: Hells Reach", 43210, Shooter, login:=True),
            G("Quake 4", 2210, Shooter, login:=True),
            G("Star Wars Jedi Knight II", 6030, Shooter, login:=True),
            G("Star Wars Jedi Knight: Jedi Academy", 6020, Shooter, login:=True),
            G("Rust", 258550, Survival, exe:="RustDedicated.exe",
              args:="-batchmode +server.port 28015 +server.level ""Procedural Map"" +server.seed 1234 +server.worldsize 3500 +server.maxplayers 50 +server.hostname ""Mein Rust Server"""),
            G("ARK: Survival Evolved", 376030, Survival, exe:="ShooterGame\Binaries\Win64\ShooterGameServer.exe", args:="TheIsland?listen?SessionName=MeinServer -server -log"),
            G("ARK: Survival Ascended", 2430930, Survival, exe:="ShooterGame\Binaries\Win64\ArkAscendedServer.exe", args:="TheIsland_WP?listen?SessionName=MeinServer -server -log"),
            G("ARK: Survival of the Fittest", 445400, Survival, login:=True),
            G("7 Days to Die", 294420, Survival, exe:="startdedicated.bat"),
            G("Valheim", 896660, Survival, exe:="valheim_server.exe",
              args:="-nographics -batchmode -name ""Mein Valheim"" -port 2456 -world ""Dedicated"" -password ""bitteaendern"" -crossplay",
              note:="Das Passwort muss mindestens 5 Zeichen haben und darf nicht im Servernamen vorkommen."),
            G("Palworld", 2394010, Survival, exe:="PalServer.exe", args:="-useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS"),
            G("Enshrouded", 2278520, Survival, exe:="enshrouded_server.exe"),
            G("V Rising", 1829350, Survival, exe:="VRisingServer.exe", args:="-persistentDataPath .\save-data -serverName ""Mein V Rising"""),
            G("Sons of the Forest", 2465200, Survival, exe:="SonsOfTheForestDS.exe"),
            G("The Forest", 556450, Survival, exe:="TheForestDedicatedServer.exe", args:="-batchmode -nographics"),
            G("Conan Exiles", 443030, Survival, exe:="ConanSandboxServer.exe", args:="-log"),
            G("Project Zomboid", 380870, Survival, exe:="StartServer64.bat"),
            G("Unturned", 1110390, Survival, exe:="Unturned.exe", args:="-batchmode -nographics +InternetServer/MeinServer"),
            G("Soulmask", 3017300, Survival),
            G("The Front", 2334200, Survival),
            G("Icarus", 2089300, Survival),
            G("Abiotic Factor", 2857200, Survival),
            G("Humanitz", 2728330, Survival),
            G("The Isle", 412680, Survival),
            G("Day of Dragons", 1088320, Survival),
            G("Beasts of Bermuda", 882430, Survival),
            G("Hurtworld", 405100, Survival),
            G("Night of the Dead", 1420710, Survival),
            G("Survive the Nights", 1502300, Survival),
            G("Last Oasis", 920720, Survival),
            G("Atlas", 1006030, Survival),
            G("Myth of Empires", 1794810, Survival),
            G("Dark and Light", 630230, Survival),
            G("PixARK", 824360, Survival),
            G("Citadel: Forged with Fire", 489650, Survival),
            G("Primal Carnage: Extinction", 336400, Survival),
            G("Reign of Kings", 381690, Survival),
            G("Don't Starve Together", 343050, Survival),
            G("Core Keeper", 1963720, Survival),
            G("Necesse", 1169370, Survival),
            G("Craftopia", 1670340, Survival),
            G("RuneScape: Dragonwilds", 4019830, Survival),
            G("Base Defense", 817300, Survival),
            G("Memories of Mars", 897590, Survival),
            G("Satisfactory", 1690800, Sandbox, exe:="FactoryServer.exe", args:="-log -unattended"),
            G("Space Engineers", 298740, Sandbox, exe:="DedicatedServer64\SpaceEngineersDedicated.exe"),
            G("Medieval Engineers", 367970, Sandbox),
            G("Empyrion - Galactic Survival", 530870, Sandbox),
            G("Avorion", 565060, Sandbox),
            G("Stationeers", 600760, Sandbox),
            G("Astroneer", 728470, Sandbox),
            G("Eco", 739590, Sandbox, exe:="EcoServer.exe"),
            G("Colony Survival", 748090, Sandbox),
            G("Barotrauma", 1026340, Sandbox, exe:="DedicatedServer.exe"),
            G("Terraria", 105600, Sandbox, login:=True, exe:="TerrariaServer.exe"),
            G("tModLoader (Terraria)", 1281930, Sandbox),
            G("Starbound", 211820, Sandbox, login:=True),
            G("Rising World", 339010, Sandbox),
            G("Interstellar Rift", 363360, Sandbox),
            G("Wurm Unlimited", 402370, Sandbox),
            G("Life is Feudal: Your Own", 320850, Sandbox),
            G("Mount & Blade II: Bannerlord", 1863440, Sandbox),
            G("Tower Unite", 439660, Sandbox),
            G("Onset", 1204170, Sandbox),
            G("Assetto Corsa", 302550, Racing, login:=True, exe:="acServer.exe"),
            G("Assetto Corsa Competizione", 1430110, Racing, login:=True, exe:="server\accServer.exe"),
            G("rFactor 2", 400300, Racing),
            G("Project CARS", 332670, Racing),
            G("Project CARS 2", 413770, Racing, login:=True),
            G("Euro Truck Simulator 2", 1948160, Racing, exe:="bin\win_x64\eurotrucks2_server.exe"),
            G("American Truck Simulator", 2239530, Racing, exe:="bin\win_x64\amtrucks_server.exe"),
            G("Wreckfest", 361580, Racing),
            G("Just Cause 2: Multiplayer", 261140, Other),
            G("Just Cause 3: Multiplayer", 619960, Other),
            G("Teeworlds", 380840, Other, login:=True),
            G("HYPERCHARGE: Unboxed", 1045940, Other),
            G("StickyBots", 974130, Other),
            G("SCUM", 3792580, Survival, exe:="SCUM\Binaries\Win64\SCUMServer.exe", args:="-log -port=7777 -MaxPlayers=64"),
            G("ASKA", 3246670, Survival, exe:="AskaServer.exe", args:="-propertiesPath ""server properties.txt""",
              note:="Einstellungen stehen in ""server properties.txt"" im Serverordner."),
            G("Sunkenland", 2667530, Survival, exe:="Sunkenland-DedicatedServer.exe",
              args:="-nographics -batchmode -maxPlayerCapacity 8 -port 27015 -queryport 27016",
              note:="Für eine bestehende Welt -worldGuid ""DEINE-WELT-GUID"" ergänzen. Nach dem ersten Download einmal aktualisieren."),
            G("VEIN", 2131400, Survival, args:="-log -Port=7777 -QueryPort=27015"),
            G("Der Herr der Ringe: Return to Moria", 3349480, Survival, exe:="MoriaServer.exe"),
            G("Frozen Flame", 1348640, Survival, args:="-log"),
            G("Smalland: Survive the Wilds", 808040, Survival, args:="-log"),
            G("Dead Matter", 2584780, Survival, exe:="DeadMatter\Binaries\Win64\DeadMatterServer-Win64-Test.exe", args:="-log"),
            G("No Love Lost", 2833050, Survival, exe:="NoLoveLost\Binaries\Win64\NoLoveLostServer-Win64-Shipping.exe", args:="-log"),
            G("No One Survived", 2329680, Survival, exe:="WRSHServer.exe", args:="-server -log"),
            G("Voyagers of Nera", 3937860, Survival, exe:="BoatGame\Binaries\Win64\BoatGameServer-Win64-Shipping.exe", args:="-log"),
            G("Windrose", 4129620, Survival, exe:="R5\Binaries\Win64\WindroseServer-Win64-Shipping.exe", args:="-log"),
            G("Zombie Survival Game Online", 2536350, Survival, exe:="ZSGO\Binaries\Win64\ZSGOServer-Win64-Shipping.exe", args:="-server -log"),
            G("Nightingale", 3796810, Survival),
            G("Subsistence", 1362640, Survival),
            G("Stormworks: Build and Rescue", 1247090, Sandbox, exe:="server64.exe"),
            G("Foundry", 2915550, Sandbox, exe:="FoundryDedicatedServer.exe", args:="-log"),
            G("Cubic Odyssey", 3858450, Sandbox, exe:="server\CubicOdysseyServer.exe"),
            G("TerraTech Worlds", 2533070, Sandbox, exe:="TT2\Binaries\Win64\TT2Server-Win64-Shipping.exe", args:="-log"),
            G("StarRupture", 3809400, Sandbox),
            G("Astro Colony", 2662210, Sandbox, args:="-log"),
            G("Portal Knights", 374040, Sandbox, login:=True, exe:="dedicated_server\pk_dedicated_server.exe", args:="-config server_config.json -log server.log"),
            G("Midnight Ghost Hunt", 1118810, Shooter, exe:="MidnightGhostHunt\Binaries\Win64\MidnightGhostHuntServer-Win64-Shipping.exe", args:="-server -log"),
            G("The Ship: Murder Party", 2403, Source, login:=True, exe:="srcds.exe", args:="-console -game ship"),
            G("Wreckfest 2", 3519390, Racing),
            G("Risk of Rain 2", 1180760, Other, exe:="Risk of Rain 2.exe"),
            G("Swords 'n Magic and Stuff", 2058450, Other, exe:="SNM2020\Binaries\Win64\SNMASServer-Win64-Shipping.exe", args:="-log")
        }
    End Function

End Class
