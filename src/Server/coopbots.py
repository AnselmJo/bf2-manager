# coopbots.py - part of BF2 Manager (installed by the app, do not edit)
# Sets bot count / distribution / difficulty per map (source: coop_server.txt) and runs
# commands from the app (coop_cmd.txt). No network port, no password.
# Python 2.3 compatible (BF2 server): no sorted(), no generator expressions,
# no "with", no "x if y else z", no "except X as e".
MODULE_VERSION = 7
import time
import sys
import host
import bf2

# Vom Installer gesetzt (Schraegstriche, keine Backslashes):
CONFIG_PATHS = ["@CFG_ABS@", "@CFG_REL@"]
POLL_SECONDS = 5
# The coop server never uses more than 32 bots (independent of sv.maxPlayers and aiDefault.ai);
# the distribution must be computed for the bots that actually spawn.
MAX_BOTS = 32
ALLOWED_PREFIXES = ("sv.", "mapList.", "admin.", "game.say")

g_started = 0
g_timer = None
g_cfgFile = None
g_cfgDir = None
g_lastCmdId = 0L
g_lastNextId = None
g_usedNextId = ""
g_adapt = 0
# After a manual map request (@nextmap, @reload) the EndGame event must not recompute the values just set,
# and a map that was only reloaded does not count as a finished round for the leaderboard.
g_skipEndUntil = 0.0
g_noRecordUntil = 0.0
# Bot count the current map was loaded with (sv.coopBotCount at round start) - the manager compares it with the bots that actually play.
g_wanted = -1


def toInt(s, dflt):
    try:
        return int(s)
    except:
        return dflt


def toLong(s, dflt):
    try:
        return long(s)
    except:
        return dflt


def roundInt(x):
    return int(x + 0.5)


def firstInt(s):
    num = ""
    for ch in s:
        if ch in "0123456789":
            num = num + ch
        elif num:
            break
    if num:
        return int(num)
    return None


def writeText(path, text, mode):
    try:
        f = open(path, mode)
        f.write(text)
        f.close()
    except:
        pass


def log(msg):
    line = time.strftime("%Y-%m-%d %H:%M:%S") + " " + str(msg)
    print("[coopbots] " + str(msg))
    if g_cfgDir is not None:
        writeText(g_cfgDir + "coop_status.txt", line + "\n", "a")


def logErr():
    try:
        e = sys.exc_info()
        log("ERROR: " + str(e[0]) + " " + str(e[1]))
    except:
        pass


def findConfig():
    for p in CONFIG_PATHS:
        try:
            f = open(p, "r")
            f.close()
            return p
        except:
            pass
    return None


def dirOf(path):
    i = path.rfind("/")
    if i < 0:
        return ""
    return path[:i + 1]


def readLines(path):
    try:
        f = open(path, "r")
        lines = f.readlines()
        f.close()
        return lines
    except:
        return []


def parseTokens(parts):
    d = {}
    for p in parts:
        i = p.find("=")
        if i > 0:
            d[p[:i].lower()] = p[i + 1:]
    return d


def readConfig():
    path = findConfig()
    if path is None:
        return None
    cfg = {"humansUS": 8, "humansMEC": 0, "default": {}, "maps": [], "next": None, "commander": None,
           "adaptive": 0, "adaptStep": 5, "adaptMax": 30}
    for raw in readLines(path):
        line = raw
        h = line.find("#")
        if h >= 0:
            line = line[:h]
        line = line.strip()
        if not line:
            continue
        parts = line.split()
        key = parts[0].lower()
        if key == "humans" and len(parts) > 1:
            cfg["humansUS"] = toInt(parts[1], 8)
            if len(parts) > 2:
                cfg["humansMEC"] = toInt(parts[2], 0)
        elif key == "default" and len(parts) > 1:
            cfg["default"][toInt(parts[1], 0)] = parseTokens(parts[2:])
        elif key == "map" and len(parts) > 3:
            cfg["maps"].append({"name": parts[1], "mode": parts[2],
                                "size": toInt(parts[3], 0), "opt": parseTokens(parts[4:])})
        elif key == "next":
            cfg["next"] = parseTokens(parts[1:])
        elif key == "commander" and len(parts) > 1:
            cfg["commander"] = toInt(parts[1], 1)
        elif key == "adaptive" and len(parts) > 1:
            cfg["adaptive"] = toInt(parts[1], 0)
            if len(parts) > 2:
                cfg["adaptStep"] = toInt(parts[2], 5)
            if len(parts) > 3:
                cfg["adaptMax"] = toInt(parts[3], 30)
    return cfg


def ratioFor(count, b):
    # Kleinste Abweichung: welche Ratio ergibt gerundet genau b Bots im MEC-Team?
    if count <= 0:
        return 50
    best = -1
    bestDist = 1000.0
    target = b * 100.0 / count
    r = 0
    while r <= 100:
        if roundInt(count * r / 100.0) == b:
            d = abs(r - target)
            if d < bestDist:
                bestDist = d
                best = r
        r = r + 1
    if best < 0:
        best = roundInt(target)
    return best


def computeBots(opt, size, hUS, hMEC):
    # Rueckgabe: (botCount, ratio, difficulty). Team 1 = MEC, Team 2 = US; Ratio = Anteil Bots im MEC-Team.
    humans = hUS + hMEC
    mode = opt.get("mode", "fixed")
    count = toInt(opt.get("count", "0"), 0)
    if mode.startswith("fill"):
        target = size
        i = mode.find(":")
        if i >= 0:
            target = toInt(mode[i + 1:], size)
        count = target - humans
    if count < 0:
        count = 0
    if count > MAX_BOTS:
        count = MAX_BOTS
    dist = opt.get("dist", "ratio:50")
    kind = dist
    val = 50
    i = dist.find(":")
    if i >= 0:
        kind = dist[:i]
        val = toInt(dist[i + 1:], 50)
    if kind == "enemy":
        x = val / 100.0
        mecTotal = roundInt((1 + x) * (humans + count) / (2 + x))
        b = mecTotal - hMEC
        if b > count:
            b = count
        if b < 0:
            b = 0
        ratio = ratioFor(count, b)
    else:
        ratio = val
    if ratio < 0:
        ratio = 0
    if ratio > 100:
        ratio = 100
    diff = toInt(opt.get("diff", "50"), 50)
    if diff < 0:
        diff = 0
    if diff > 100:
        diff = 100
    return (count, ratio, diff)


def runCmd(cmd):
    try:
        r = str(host.rcon_invoke(cmd))
    except:
        logErr()
        r = "FEHLER"
    return r


def queryInt(cmd):
    return firstInt(runCmd(cmd))


def countPlayers():
    # Rueckgabe: (MenschenUS, MenschenMEC, BotsUS, BotsMEC) - Team 1 = MEC, Team 2 = US
    c = [0, 0, 0, 0]
    try:
        for p in bf2.playerManager.getPlayers():
            t = 0
            try:
                t = p.getTeam()
            except:
                pass
            i = 0
            if t == 1:
                i = 1
            if p.isAIPlayer():
                i = i + 2
            c[i] = c[i] + 1
    except:
        pass
    return (c[0], c[1], c[2], c[3])


def nextIndex(cfg):
    n = len(cfg["maps"])
    if n == 0:
        return None
    idx = queryInt("admin.nextLevel")
    if idx is None or idx >= n:
        cur = queryInt("mapList.currentMap")
        if cur is None:
            cur = 0
        idx = (cur + 1) % n
    return idx


def applyForNext(reason):
    global g_lastNextId, g_usedNextId
    cfg = readConfig()
    if cfg is None:
        log("coop_server.txt not readable")
        return 0
    idx = nextIndex(cfg)
    if idx is None:
        log("no maps in coop_server.txt")
        return 0
    m = cfg["maps"][idx]
    if m["mode"] != "gpm_coop":
        log("next map " + m["name"] + " is not coop - no change")
        return 0
    opt = {}
    if m["size"] in cfg["default"]:
        opt.update(cfg["default"][m["size"]])
    opt.update(m["opt"])
    usedNext = 0
    if cfg["next"] is not None:
        nid = cfg["next"].get("id", "")
        if nid != g_lastNextId:
            opt.update(cfg["next"])
            g_lastNextId = nid
            g_usedNextId = nid
            usedNext = 1
    pc = countPlayers()
    hUS = pc[0]
    hMEC = pc[1]
    if hUS + hMEC < 1:
        hUS = cfg["humansUS"]
        hMEC = cfg["humansMEC"]
    res = computeBots(opt, m["size"], hUS, hMEC)
    diff = res[2]
    if cfg["adaptive"]:
        diff = diff + g_adapt
        if diff < 0:
            diff = 0
        if diff > 100:
            diff = 100
    runCmd("sv.coopBotCount " + str(res[0]))
    runCmd("sv.coopBotRatio " + str(res[1]))
    runCmd("sv.coopBotDifficulty " + str(diff))
    text = "[" + reason + "] next map #" + str(idx) + " " + m["name"] + " " + str(m["size"])
    text = text + ": bots=" + str(res[0]) + " ratio=" + str(res[1]) + " difficulty=" + str(diff)
    if cfg["adaptive"]:
        text = text + " (adaptive " + str(g_adapt) + ")"
    text = text + " (humans US=" + str(hUS) + " MEC=" + str(hMEC) + ")"
    if usedNext:
        text = text + " [one-time values]"
    log(text)
    return 1


def setCommander(on, reason):
    # host.sh_setEnableCommander from the BF2 Python reference
    try:
        host.sh_setEnableCommander(on)
        if on:
            log("commander enabled (" + reason + ")")
        else:
            log("commander disabled (" + reason + ")")
    except:
        logErr()


def applyCommander(reason):
    cfg = readConfig()
    if cfg is None or cfg["commander"] is None:
        return
    setCommander(cfg["commander"], reason)


def recordRound():
    # Writes the round result for the evening leaderboard and adjusts the adaptive difficulty
    global g_adapt
    t1 = queryTickets(1)
    t2 = queryTickets(2)
    winner = 0
    if t1 > t2:
        winner = 1
    elif t2 > t1:
        winner = 2
    pc = countPlayers()
    humanTeam = 2
    if pc[1] > pc[0]:
        humanTeam = 1
    cfg = readConfig()
    if cfg is not None and cfg["adaptive"] and winner != 0:
        step = cfg["adaptStep"]
        mx = cfg["adaptMax"]
        if winner == humanTeam:
            g_adapt = g_adapt + step
        else:
            g_adapt = g_adapt - step
        if g_adapt > mx:
            g_adapt = mx
        if g_adapt < -mx:
            g_adapt = -mx
        log("adaptive difficulty now " + str(g_adapt) + " (winner team " + str(winner) + ")")
    mapName = ""
    try:
        mapName = str(bf2.gameLogic.getMapName())
    except:
        pass
    text = "round|" + str(int(time.time())) + "|" + mapName + "|" + str(winner) + "|" + str(t1) + "|" + str(t2) + "|" + str(humanTeam) + "\n"
    try:
        for p in bf2.playerManager.getPlayers():
            if p.isAIPlayer():
                continue
            sc = [0, 0, 0]
            try:
                sc = [p.score.score, p.score.kills, p.score.deaths]
            except:
                pass
            nm = str(p.getName()).replace("|", "/")
            text = text + "player|" + nm + "|" + str(p.getTeam()) + "|" + str(sc[0]) + "|" + str(sc[1]) + "|" + str(sc[2]) + "\n"
    except:
        pass
    if g_cfgDir is not None:
        writeText(g_cfgDir + "coop_rounds.txt", text, "a")


def queryTickets(team):
    try:
        return int(bf2.gameLogic.getTickets(team))
    except:
        return 0


def onGameStatusChanged(status):
    global g_wanted
    try:
        log("GameStatus=" + str(status))
        if status == bf2.GameStatus.EndGame:
            if time.time() >= g_noRecordUntil:
                try:
                    recordRound()
                except:
                    logErr()
            if time.time() < g_skipEndUntil:
                log("EndGame: values were set by the manual map change, kept")
            else:
                applyForNext("EndGame")
        elif status == bf2.GameStatus.PreGame or status == bf2.GameStatus.Playing:
            applyCommander("round start")
            if status == bf2.GameStatus.PreGame or g_wanted < 0:
                w = queryInt("sv.coopBotCount")
                if w is not None:
                    g_wanted = w
    except:
        logErr()


def writeLive():
    pc = countPlayers()
    mapName = ""
    try:
        mapName = str(bf2.gameLogic.getMapName())
    except:
        pass
    text = "updated=" + str(int(time.time())) + "\n"
    text = text + "map=" + mapName + "\n"
    text = text + "humans=" + str(pc[0] + pc[1]) + "\n"
    text = text + "bots=" + str(pc[2] + pc[3]) + "\n"
    text = text + "humansUS=" + str(pc[0]) + "\n"
    text = text + "humansMEC=" + str(pc[1]) + "\n"
    text = text + "botsUS=" + str(pc[2]) + "\n"
    text = text + "botsMEC=" + str(pc[3]) + "\n"
    text = text + "nextUsed=" + str(g_usedNextId) + "\n"
    text = text + "module=" + str(MODULE_VERSION) + "\n"
    text = text + "adapt=" + str(g_adapt) + "\n"
    text = text + "botsWanted=" + str(g_wanted) + "\n"
    try:
        for p in bf2.playerManager.getPlayers():
            if p.isAIPlayer():
                continue
            nm = str(p.getName()).replace("|", "/")
            sc = [0, 0, 0]
            try:
                sc = [p.score.score, p.score.kills, p.score.deaths]
            except:
                pass
            ping = 0
            try:
                ping = p.getPing()
            except:
                pass
            text = text + "player=" + str(p.index) + "|" + str(p.getTeam()) + "|" + nm + "|" + str(sc[0]) + "|" + str(sc[1]) + "|" + str(sc[2]) + "|" + str(ping) + "\n"
    except:
        pass
    writeText(g_cfgDir + "coop_live.txt", text, "w")


def isAllowed(cmd):
    low = cmd.lower()
    if cmd == "@nextmap" or cmd == "@reload" or cmd.startswith("@commander ") or cmd.startswith("@team "):
        return 1
    for p in ALLOWED_PREFIXES:
        if low.startswith(p.lower()):
            return 1
    return 0


def switchTeam(idx):
    # Player with this index changes to the other team; a living player respawns there
    try:
        for p in bf2.playerManager.getPlayers():
            if p.index == idx:
                old = p.getTeam()
                new = 2
                if old == 2:
                    new = 1
                p.setTeam(new)
                try:
                    if p.isAlive():
                        v = p.getDefaultVehicle()
                        if v:
                            v.setDamage(0.0)
                except:
                    pass
                return "team " + str(old) + " -> " + str(new)
        return "player not found"
    except:
        logErr()
        return "error"


def maxCmdId():
    best = 0L
    for raw in readLines(g_cfgDir + "coop_cmd.txt"):
        parts = raw.strip().split(None, 1)
        if parts:
            n = toLong(parts[0], 0)
            if n > best:
                best = n
    return best


def processCommands():
    global g_lastCmdId, g_skipEndUntil, g_noRecordUntil
    top = g_lastCmdId
    for raw in readLines(g_cfgDir + "coop_cmd.txt"):
        parts = raw.strip().split(None, 1)
        if len(parts) < 2:
            continue
        cid = toLong(parts[0], 0)
        if cid <= g_lastCmdId:
            continue
        if cid > top:
            top = cid
        cmd = parts[1].strip()
        if not isAllowed(cmd):
            log("command rejected: " + cmd)
            continue
        if cmd == "@nextmap":
            applyForNext("manual")
            g_skipEndUntil = time.time() + 20.0
            res = runCmd("admin.runNextLevel")
        elif cmd == "@reload":
            # reload the current map with the (one-time) bot values; players stay connected
            cur = queryInt("mapList.currentMap")
            if cur is None:
                cur = 0
            runCmd("admin.nextLevel " + str(cur))
            applyForNext("reload")
            g_skipEndUntil = time.time() + 20.0
            g_noRecordUntil = time.time() + 20.0
            res = runCmd("admin.runNextLevel")
        elif cmd.startswith("@commander "):
            setCommander(toInt(cmd[11:].strip(), 1), "live")
            res = "ok"
        elif cmd.startswith("@team "):
            res = switchTeam(toInt(cmd[6:].strip(), -1))
        else:
            res = runCmd(cmd)
        res = res.replace("\r", "").replace("\n", " | ")
        log("command " + cmd + " -> " + res[:1500])
    g_lastCmdId = top


def onTimer(data=None):
    try:
        writeLive()
        processCommands()
    except:
        logErr()


def init():
    global g_started, g_timer, g_cfgFile, g_cfgDir, g_lastCmdId
    if g_started:
        return
    g_started = 1
    try:
        g_cfgFile = findConfig()
        if g_cfgFile is None:
            print("[coopbots] coop_server.txt not found - inactive")
            return
        g_cfgDir = dirOf(g_cfgFile)
        writeText(g_cfgDir + "coop_status.txt", "", "w")
        log("coopbots " + str(MODULE_VERSION) + " started, config: " + g_cfgFile)
        g_lastCmdId = maxCmdId()
        host.registerGameStatusHandler(onGameStatusChanged)
        applyCommander("Start")
        try:
            g_timer = bf2.Timer(onTimer, POLL_SECONDS, 1)
            g_timer.setRecurring(POLL_SECONDS)
        except:
            log("bf2.Timer not available - live commands disabled")
    except:
        logErr()
