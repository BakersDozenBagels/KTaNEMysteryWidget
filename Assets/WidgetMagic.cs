using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

public class WidgetMagic : MonoBehaviour {
    public KMBombModule ActiveModule;
    public GameObject CoverPrefab;
    public TextMesh IDMesh, DisplayMesh;
    public KMBossModule BossManager;
    public KMSelectable Button;
    public KMAudio Audio;
    public KMBombInfo Info;
    public KMRuleSeedable RuleSeed;

    private float scaler = 0.75f; 

    private GameObject Cover;

    private int id;

    private bool Displayed = false, Ready = false;

    private List<string> valid = new List<string>(), solvedModules = new List<string>();
    private string current;

    private bool _awake = false;

    private int pressedOn;

    //Souv variables
    private string keyModule = null;
    private string hiddenType = null;
    private bool isSolved = false;
    private bool isAutoSolved = false;
    private List<String> preferredEdgework = new List<string>();

    sealed class BombInfo
    {
        public List<String> IgnoredSN = new List<string>(), IgnoredOther = new List<string>(), KnownModules = new List<string>();
        public string[] IgnoredAlways;
        public bool SNCovered = false, OtherCovered = false;
        public bool SNSelf = false, OtherSelf = false;
        public List<Component> edgework, allEdgework;
        public bool toBeGenerated = true;
        public int idNext = 1;
    }

    private static readonly Dictionary<string, BombInfo> _infos = new Dictionary<string, BombInfo>();
    private BombInfo _info;

    private void Reset()
    {
        scaler = 0.75f;
        _info.IgnoredSN = new List<string>(); _info.IgnoredOther = new List<string>(); _info.KnownModules = new List<string>();
        _info.SNCovered = false; _info.OtherCovered = false;
        _info.SNSelf = false; _info.OtherSelf = false;
        _info.toBeGenerated = true;
        Displayed = false; Ready = false;
        valid = new List<string>(); solvedModules = new List<string>();
        _awake = false;
    }

    // Use this for initialization
    void Start () {
        if (_infos.ContainsKey(Info.GetSerialNumber())) { _info = _infos[Info.GetSerialNumber()]; }
        else
        {
            _infos.Add(Info.GetSerialNumber(), new BombInfo());
            _info = _infos[Info.GetSerialNumber()];
        }
        StartCoroutine(MakeCover());
        Info.OnBombExploded += delegate () { Reset(); };
        Info.OnBombSolved += delegate () { if (Info.GetSolvedModuleNames().Count == Info.GetSolvableModuleNames().Count) { Reset(); } };
	}

    private IEnumerator MakeCover()
    {
        yield return null;
        if (_info.toBeGenerated)
        {
            List<Component> possibleEdgework = ActiveModule.transform.root.GetComponentsInChildren<Component>().Where(x => x.GetType().Name == "Transform" || x.GetType().Name == "KMWidget").ToList();
            Regex rx = new Regex(@"SerialNumber\(Clone\)|BatteryWidget\(Clone\)|IndicatorWidget\(Clone\)|PortWidget\(Clone\)", RegexOptions.IgnoreCase);
            Regex seed = new Regex(@"seed", RegexOptions.IgnoreCase);
            _info.edgework = possibleEdgework.Where(x => (rx.IsMatch(x.name) || x.GetType().Name == "KMWidget") && !seed.IsMatch(x.name)).ToList();
            _info.allEdgework = _info.edgework;
            GenerateIgnored();
            _info.IgnoredAlways = GetIgnored();
            var l = _info.IgnoredAlways.ToList();
            l.AddRange(new string[] { "Mystery Widget", "Cookie Jars", "Divided Squares", "Encrypted Hangman", "Encryption Bingo", "Four-Card Monte", "Hogwarts", "The Heart", "The Swan", "Button Messer", "Random Access Memory", "Turn The Keys", "Tech Support", "Forget Perspective", "Security Council", "Bamboozling Time Keeper", "OmegaDestroyer", "The Very Annoying Button", "Forget Me Not", "Turn The Key", "Forget It Not", "42", "A>N<D", "Shoddy Chess", "The Time Keeper", "Brainf---", "501", "Forget Me Later", "Ultimate Custom Night", "Forget Any Color", "The Twin", "Timing is Everything", "Forget Them All", "Forget Us Not", "Password Destroyer", "Simon Forgets", "Forget Maze Not", "Forget Everything", "OmegaForget", "Übermodule", "Purgatory", "Forget Enigma", "Forget This", "RPS Judging", "Keypad Directionality", "Souvenir", "Forget Infinity", "Multitask", "Iconic", "Tallordered Keys", "14", "Simon's Stages", "The Troll", "Forget The Colors", "Organization", "Floor Lights", "Whiteout", "Don't Touch Anything", "Kugelblitz", "Busy Beaver", "Encrypted Hangman", "Turn The Keys", "Button Messer", "Cookie Jars", "Encryption Bingo", "Tech Support", "Random Access Memory", "Hogwarts", "Four-Card Monte", "Divided Squares", "The Swan", "Black Arrows", "Zener Cards", "Simp Me Not" });
            _info.IgnoredAlways = l.ToArray();
            _info.toBeGenerated = false;
        }
        foreach (Component n in _info.allEdgework)
        {
            Regex rx = new Regex("serial", RegexOptions.IgnoreCase);
            Regex rx1 = new Regex(@"battery", RegexOptions.IgnoreCase);
            Regex rx2 = new Regex(@"port", RegexOptions.IgnoreCase);
            Regex rx3 = new Regex(@"indicator", RegexOptions.IgnoreCase);
            if (rx.IsMatch(n.name)) { preferredEdgework.Add("Serial Number"); }
            else if (rx2.IsMatch(n.name)) { preferredEdgework.Add("Battery"); }
            else if (rx2.IsMatch(n.name)) { preferredEdgework.Add("Port"); }
            else if (rx3.IsMatch(n.name)) { preferredEdgework.Add("Indicator"); }
            else { preferredEdgework.Add("Modded Widget"); }
        }
        if (_info.edgework.Count == 0)
        {
            Cover = Instantiate(CoverPrefab);
            Cover.SetActive(false);
            IDMesh.text = "ERR";
            DisplayMesh.text = "Press to solve.";
            Debug.LogFormat("[Mystery Widget #{0}] Not enough widgets to cover! Press to solve.", id);
            isAutoSolved = true;
            Button.OnInteract += delegate () { isSolved = true; ActiveModule.HandlePass(); Debug.LogFormat("[Mystery Widget #{0}] Solved by too few widgets.", id); return false; };
        }
        else
        {
            Component Selected = _info.edgework.PickRandom();
            Cover = Instantiate(CoverPrefab, Selected.transform);
            Regex rx = new Regex("serial", RegexOptions.IgnoreCase);
            Cover.GetComponentInChildren<TextMesh>().text = _info.idNext.ToString();
            IDMesh.text = _info.idNext.ToString();
            id = _info.idNext;
            _info.idNext++;
            if (rx.IsMatch(Selected.gameObject.name))
            {
                _info.SNCovered = true;
                _info.SNSelf = true;
                Debug.LogFormat("[Mystery Widget #{0}] Covered the serial number.", id);
                hiddenType = "Serial Number";
            }
            else
            {
                _info.OtherCovered = true;
                _info.OtherSelf = true;
                Debug.LogFormat("[Mystery Widget #{0}] Covered a port, battery, indicator, or modded widget.", id);
                Regex rx1 = new Regex(@"battery", RegexOptions.IgnoreCase);
                Regex rx2 = new Regex(@"port", RegexOptions.IgnoreCase);
                Regex rx3 = new Regex(@"indicator", RegexOptions.IgnoreCase);
                if (rx1.IsMatch(Selected.gameObject.name))
                {
                    hiddenType = "Battery";
                }
                else if (rx2.IsMatch(Selected.gameObject.name))
                {
                    hiddenType = "Port";
                }
                else if (rx3.IsMatch(Selected.gameObject.name))
                {
                    hiddenType = "Indicator";
                }
                else
                {
                    hiddenType = "Modded Widget";
                }
                Debug.LogFormat("[Mystery Widget #{0}] Specifically, a {1}.", id, hiddenType);
            }
            _info.edgework.Remove(Selected);
            Button.OnInteract += delegate () { HandlePressDown(); return false; };
            Button.OnInteractEnded += delegate () { HandlePressUp(); };
        }
        Button.OnInteract += delegate () { Button.AddInteractionPunch(0.1f); Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform); return false; };
        Button.OnInteractEnded += delegate () { Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform); };
        valid = Info.GetSolvableModuleNames().Where(x => !_info.IgnoredAlways.Contains(x) && _info.KnownModules.Contains(x)).ToList();
        if (_info.SNCovered) { valid = valid.Where(x => !_info.IgnoredSN.Contains(x)).ToList(); }
        if (_info.OtherCovered && !_info.SNSelf) { valid = valid.Where(x => !_info.IgnoredOther.Contains(x)).ToList(); }
        valid = valid.Shuffle().Take((int)Math.Floor(valid.Count * scaler)).ToList();
        if (Info.GetSolvableModuleNames().Contains("Button Messer") || Info.GetSolvableModuleNames().Contains("Organization") || Info.GetSolvableModuleNames().Contains("Turn The Keys"))
        {
            valid = new List<string>();
            isAutoSolved = true;
            Debug.LogFormat("[Mystery Widget #{0}] There is a bad module, meaning this must auto-solve.", id);
        }
        else if (!RuleSeed.GetRNG().Seed.Equals(1))
        {
            valid = new List<string>();
            isAutoSolved = true;
            Debug.LogFormat("[Mystery Widget #{0}] Rule seed is not 1, meaning this must auto-solve.", id);
        }
        else if (Info.GetSolvableModuleNames().Contains("Mystery Module"))
        {
            foreach(KMBombModule l in FindObjectsOfType<KMBombModule>())
            {
                foreach(Component c in l.GetComponents<Component>())
                {
                    if (c.GetType().Name.Equals("MysteryModuleScript"))
                    {
                        BindingFlags f = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
                        try
                        {
                            Debug.LogFormat("[Mystery Widget #{0}] Attempting to retrieve mystery module's mystified module...", id);
                            c.GetType().GetField("mystifiedModule", f).GetValue(c);
                        }
                        catch
                        {
                            valid = new List<string>();
                            isAutoSolved = true;
                            Debug.LogFormat("[Mystery Widget #{0}] Error finding module mystified by mystery module, auto-solving instead.", id);
                            goto error;
                        }
                        if (c.GetType().GetField("mystifiedModule", f).GetValue(c) == null)
                        {
                            yield return new WaitUntil(() => c.GetType().GetField("mystifiedModule", f).GetValue(c) != null);
                        }
                        try
                        {
                            valid.Remove(c.GetType().GetField("mystifiedModule", f).GetValue(c).GetType().GetField("ModuleDisplayName", f).GetValue(c.GetType().GetField("mystifiedModule", f).GetValue(c)).ToString());
                            Debug.LogFormat("[Mystery Widget #{0}] Your chosen stages are: {1}.", id, valid.Join(", "));
                        }
                        catch
                        {
                            valid = new List<string>();
                            isAutoSolved = true;
                            Debug.LogFormat("[Mystery Widget #{0}] Error finding module mystified by mystery module, auto-solving instead.", id);
                            goto error;
                        }
                    }
                }
            }
        }
        else if(valid.Count == 0)
        {
            isAutoSolved = true;
            Debug.LogFormat("[Mystery Widget #{0}] No stages generated, autosolving instead.", id);
        }
        else
        {
            Debug.LogFormat("[Mystery Widget #{0}] Your chosen stages are: {1}.", id, valid.Join(", "));
        }
    error:
        if (isAutoSolved)
        {
            Cover.transform.localScale = new Vector3(0f, 0f, 0f);
        }
        _awake = true;
    }

    private void HandlePressDown()
    {
        pressedOn = (int)Math.Floor(Info.GetTime());
        Debug.LogFormat("[Mystery Widget #{0}] Pressed at {1}.", id, pressedOn, pressedOn - 3);
    }

    private void HandlePressUp()
    {
        Debug.LogFormat("[Mystery Widget #{0}] Released on: {1}.", id, (int)Math.Floor(Info.GetTime()));
        if(Ready && (pressedOn == (int)Math.Floor(Info.GetTime()) + 3 || pressedOn == (int)Math.Floor(Info.GetTime()) - 3))
        {
            Debug.LogFormat("[Mystery Widget #{0}] Correct! Solve.", id);
            isSolved = true;
            ActiveModule.HandlePass();
            StartCoroutine(UnCover());
            Ready = false;
        }
        else if(current != null && (pressedOn == (int)Math.Floor(Info.GetTime()) + 1 || pressedOn == (int)Math.Floor(Info.GetTime()) - 1))
        {
            Debug.LogFormat("[Mystery Widget #{0}] Failswitch! Taking off time.", id);
            TimeRemaining.FromModule(ActiveModule, Info.GetTime() * 0.8f);
            Displayed = false;
            valid.Remove(current);
        }
        else { Debug.LogFormat("[Mystery Widget #{0}] Wrong! Strike.", id); ActiveModule.HandleStrike(); }
    }

    private void GenerateIgnored()
    {
        //NAME; SN; IND; BAT; BAT; PRT; PRT; ANY
        Module m = new Module("", "", "", "", "", "", "", "");
        m.Generate();
        Regex yes = new Regex(@"Yes", RegexOptions.IgnoreCase);
        foreach (Module mod in m.ModuleList)
        {
            if(yes.IsMatch(mod.p1)) { _info.IgnoredSN.Add(mod.name); }
            if(yes.IsMatch(mod.p2) || yes.IsMatch(mod.p3) || yes.IsMatch(mod.p4) || yes.IsMatch(mod.p5) || yes.IsMatch(mod.p6)) { _info.IgnoredOther.Add(mod.name); }
            _info.KnownModules.Add(mod.name);
        }
    }

    private string[] GetIgnored()
    {
        return BossManager.GetIgnoredModules("Mystery Widget", new string[] { "Mystery Widget", "Cookie Jars", "Divided Squares", "Encrypted Hangman", "Encryption Bingo", "Four-Card Monte", "Hogwarts", "The Heart", "The Swan", "Button Messer", "Random Access Memory", "Turn The Keys", "Tech Support", "+FullBoss", "+SemiBoss" });
    }

    private bool GenerateDisplay()
    {
        Displayed = true;
        if (valid.Count > 0)
        {
            Debug.LogFormat("[Mystery Widget #{0}] Next display is: {1}.", id, current);
            current = valid.PickRandom();
            valid.Remove(current);
            DisplayMesh.text = current;
            if (keyModule == null)
                keyModule = current;
            return false;
        }
        current = null;
        return true;
    }

    void Update()
    {
        if (_awake)
        {
            if (!Displayed)
            {
                if (GenerateDisplay())
                {
                    Ready = true;
                    DisplayMesh.text = "Ready to solve...";
                    Debug.LogFormat("[Mystery Widget #{0}] All stages done! Ready to solve.", id, current);
                    StartCoroutine(ReadyFlash());
                }
            }
            else
            {
                if (Info.GetSolvedModuleNames().Count > solvedModules.Count)
                {
                    List<string> x = Info.GetSolvedModuleNames();
                    foreach (string solved in solvedModules)
                    {
                        x.Remove(solved);
                    }
                    foreach (string a in x)
                    {
                        if (a == current)
                        {
                            Displayed = false;
                            Debug.LogFormat("[Mystery Widget #{0}] {1} solved! Moving on...", id, current);
                        }
                        if (valid.Contains(a)) { valid.Remove(a); }
                        solvedModules.Add(a);
                    }
                }
            }
        }
    }

    private IEnumerator UnCover()
    {
        for (int i = 0; i < 10; i++)
        {
            Cover.transform.localScale -= new Vector3(0.1f, 0.1f, 0.1f);
            yield return new WaitForSeconds(0.05f);
        }
        Destroy(Cover);
    }

    private IEnumerator ReadyFlash()
    {
        while (Ready)
        {
            DisplayMesh.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            DisplayMesh.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }
        DisplayMesh.text = "Congrats!";
    }

    //TP handling
    void TwitchHandleForcedSolve()
    {
        if (isSolved) return;
        isSolved = true;
        DisplayMesh.text = "Congrats!";
        ActiveModule.HandlePass();
        Ready = false;
        StartCoroutine(UnCover());
        Displayed = true;
        valid = new List<string>();
        current = null;
        Reset();
    }
    bool TwitchShouldCancelCommand;
    string TwitchHelpMessage = "Use \"!{0} hold 3\" to hold across three timer ticks.";
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Trim();
        Regex rx = new Regex(@"^hold\s+\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!rx.IsMatch(command)) yield break;
        Regex rxd = new Regex(@"\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        int userInput = int.Parse(rxd.Match(command).Value);

        Button.OnInteract();
        var time = (int)Info.GetTime();
        while (time - userInput != (int)Info.GetTime() && time + userInput != (int)Info.GetTime())
        {
            if (TwitchShouldCancelCommand)
            {
                Button.OnInteractEnded();
                yield return "cancelled";
            }
            yield return new WaitForSeconds(0.1f);
        }
        Button.OnInteractEnded();
        yield return null;
    }
}