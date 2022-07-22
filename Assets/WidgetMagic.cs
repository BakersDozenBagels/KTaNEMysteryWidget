using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using RNG = UnityEngine.Random;

public class WidgetMagic : MonoBehaviour
{
    public KMBombModule ActiveModule;
    public GameObject CoverPrefab;
    public TextMesh IDMesh;
    public KMSelectable Button;
    public KMAudio Audio;
    public KMBombInfo Info;
    public Renderer[] SpriteSlots;
    public Texture[] Sprites;

    private GameObject Cover;

    private int id = ++_idc, coverId;
    private static int _idc;

    private bool _awake = false;

    private bool _isSolved = false;

    private List<Func<float, float, bool>> _expectedActions;
    private List<int> _slotOrder;
    private List<int> _symbols;

    private float _lastHold = -1;

    sealed class BombInfo
    {
        public List<Component> edgework;
        public bool toBeGenerated = true;
        public int idNext = 1;
    }

    private static readonly Dictionary<string, BombInfo> _infos = new Dictionary<string, BombInfo>();
    private BombInfo _info;

    // Use this for initialization
    void Start()
    {
        if(_infos.ContainsKey(Info.GetSerialNumber())) { _info = _infos[Info.GetSerialNumber()]; }
        else
        {
            _infos.Add(Info.GetSerialNumber(), new BombInfo());
            _info = _infos[Info.GetSerialNumber()];
        }
        ActiveModule.OnActivate += () => Activate();

        Button.OnInteract += Press;
        Button.OnInteractEnded += Release;
    }

    private void Release()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, Button.transform);
        if(!_awake || _lastHold < 0 || _isSolved || _expectedActions.Count == 0)
            return;

        if(_expectedActions[0](_lastHold, Info.GetTime()))
        {
            StartCoroutine(Fade(SpriteSlots[_slotOrder[0]], _expectedActions.Count == 1));

            _expectedActions = _expectedActions.Skip(1).ToList();
            _slotOrder = _slotOrder.Skip(1).ToList();

            Debug.LogFormat("[Mystery Widget #{0}] That input was correct (Held at {1}, released at {2}). {3} Remain.", id, _lastHold, Info.GetTime(), _expectedActions.Count);
        }
        else
        {
            Debug.LogFormat("[Mystery Widget #{0}] Bad input (Held at {1}, released at {2}). Strike!", id, _lastHold, Info.GetTime());
            ActiveModule.HandleStrike();
        }

        _lastHold = -1;
    }

    private IEnumerator Fade(Renderer renderer, bool solve)
    {
        float time = Time.time;
        while(Time.time - time < 2f)
        {
            yield return null;
            renderer.material.color = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0f), (Time.time - time) / 2);
        }
        renderer.enabled = false;

        if(solve)
        {
            Debug.LogFormat("[Mystery Widget #{0}] Module solved, uncovering hidden widget.", id);
            StartCoroutine(UnCover());
            _isSolved = true;
            ActiveModule.HandlePass();
        }
    }

    private bool Press()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, Button.transform);
        if(!_awake || _isSolved)
            return false;

        _lastHold = Info.GetTime();

        return false;
    }

    private void Activate()
    {
        if(_info.toBeGenerated)
        {
            List<Component> possibleEdgework = transform.root.GetComponentsInChildren<Component>().Where(x => x != null && (x.GetType().Name == "Transform" || x.GetType().Name == "KMWidget")).ToList();
            Regex rx = new Regex(@"SerialNumber\(Clone\)|BatteryWidget\(Clone\)|IndicatorWidget\(Clone\)|PortWidget\(Clone\)", RegexOptions.IgnoreCase);
            Regex seed = new Regex(@"seed", RegexOptions.IgnoreCase);
            _info.edgework = possibleEdgework.Where(y => y != null && (rx.IsMatch(y.name) || y.GetType().Name == "KMWidget") && !seed.IsMatch(y.name)).ToList();
            _info.toBeGenerated = false;
        }
        if(_info.edgework.Count == 0)
        {
            Cover = Instantiate(CoverPrefab);
            Cover.SetActive(false);
            IDMesh.text = "ERR";
            Debug.LogFormat("[Mystery Widget #{0}] Not enough widgets to cover! Covering nothing.", id);
        }
        else
        {
            Component Selected = _info.edgework.PickRandom();
            Cover = Instantiate(CoverPrefab, Selected.transform);
            Regex rx = new Regex("serial", RegexOptions.IgnoreCase);
            Cover.GetComponentInChildren<TextMesh>().text = _info.idNext.ToString();
            IDMesh.text = _info.idNext.ToString();
            coverId = _info.idNext;
            _info.idNext++;
            Debug.LogFormat("[Mystery Widget #{0}] Covered an object called {1}.", id, Selected.name);
            _info.edgework.Remove(Selected);
        }
        Button.OnInteract += delegate () { Button.AddInteractionPunch(0.1f); Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform); return false; };
        Button.OnInteractEnded += delegate () { Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform); };

        GeneratePuzzle();

        _awake = true;
    }

    private void GeneratePuzzle()
    {
        _symbols = Enumerable.Repeat(0, 7).Select(x => RNG.Range(0, 12)).ToList();

        for(int i = 0; i < 7; i++)
            SpriteSlots[i].material.mainTexture = Sprites[_symbols[i]];

        Debug.LogFormat("[Mystery Widget #{0}] The displayed sprites are: {1}", id, _symbols.Select(i => i + 1).Join(", "));

        _slotOrder = Enumerable.Range(0, 7).Where(i => _symbols[i] % 2 == 0).Concat(Enumerable.Range(0, 7).Where(i => _symbols[i] % 2 == 1).Reverse()).ToList();

        _expectedActions = _slotOrder.Select<int, Func<float, float, bool>>((i, ix) =>
           {
               switch(_symbols[i])
               {
                   case 0:
                   case 1:
                       return (h, r) => (int)h == (int)r && (int)h % 10 == i + 1;
                   case 2:
                   case 3:
                       return (h, r) => (int)h == (int)r && (int)h % 10 == ix;
                   case 4:
                   case 5:
                       return (h, r) => Mathf.Abs((int)h - (int)r) == 1;
                   case 6:
                   case 7:
                       return (h, r) => Mathf.Abs((int)h - (int)r) == 2;
                   case 8:
                   case 9:
                       return (h, r) => (int)h == (int)r && ((int)h % 60) / 10 == (int)h % 10;
                   case 10:
                   case 11:
                       return (h, r) => (int)h == (int)r && IsPrime((int)h % 60);
               }
               throw new ArgumentOutOfRangeException("Bad instruction index " + i);
           }).ToList();
    }

    private bool IsPrime(int x)
    {
        return new List<int>() { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59 }.Contains(x);
    }

    private IEnumerator UnCover()
    {
        for(int i = 0; i < 10; i++)
        {
            Cover.transform.localScale -= new Vector3(0.1f, 0.1f, 0.1f);
            yield return new WaitForSeconds(0.05f);
        }
        Destroy(Cover);
    }

    string TwitchHelpMessage = "Use \"!{0} at 25 hold 2\" to hold the button when the timer displays 25 seconds across 2 timer ticks. Either half is optional. A one digit number will be treated as only the last digit of the timer, use \"0#\" for the whole timer.";
    bool ZenModeActive;

    IEnumerator TwitchHandleForcedSolve()
    {
        while(_expectedActions.Count > 0)
        {
            IEnumerator cmd = null;
            switch(_symbols[_slotOrder[0]])
            {
                case 0:
                case 1:
                    cmd = ProcessTwitchCommand("at " + (_slotOrder[0] + 1));
                    break;
                case 2:
                case 3:
                    cmd = ProcessTwitchCommand("at " + (7 - _expectedActions.Count));
                    break;
                case 4:
                case 5:
                    cmd = ProcessTwitchCommand("hold 1");
                    break;
                case 6:
                case 7:
                    cmd = ProcessTwitchCommand("hold 2");
                    break;
                case 8:
                case 9:
                    float target = Time.time;
                    int realTarget = (int)target;

                    if(target % 1f < .2f && target % 1 > 0.8f)
                        realTarget -= ZenModeActive ? -1 : 1;

                    while((realTarget % 60) / 10 != realTarget % 10)
                        realTarget -= ZenModeActive ? -1 : 1;

                    realTarget %= 60;

                    if(realTarget < 0)
                        goto error;

                    cmd = ProcessTwitchCommand("at " + (realTarget < 10 ? "0" : "") + realTarget);
                    break;
                case 10:
                case 11:
                    target = Time.time;
                    realTarget = (int)target;

                    if(target % 1f < .2f && target % 1 > 0.8f)
                        realTarget -= ZenModeActive ? -1 : 1;

                    while(!IsPrime(realTarget % 60))
                        realTarget -= ZenModeActive ? -1 : 1;

                    realTarget %= 60;

                    if(realTarget < 0)
                        goto error;

                    cmd = ProcessTwitchCommand("at " + (realTarget < 10 ? "0" : "") + realTarget);
                    break;
            }

            while(cmd.MoveNext())
            {
                if(cmd.Current is string)
                    yield return true;
                else
                    yield return cmd.Current;
            }
            yield return true;
        }

        while(!_isSolved)
            yield return true;

        yield break;

        error:
        Debug.LogFormat("[Mystery Widget #{0}] Error while autosolving!", id);
        StartCoroutine(UnCover());
        ActiveModule.HandlePass();
        _isSolved = true;
    }

    IEnumerator ProcessTwitchCommand(string command)
    {
        Regex r = new Regex(@"^((at\s+[0-5]?\d(\s+hold\s+\d)?)|(hold\s+\d(\s+at\s+[0-5]?\d)?))$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if(r.IsMatch(command.Trim()))
        {
            int target = -1;
            bool fuzzy = false;
            int duration = 0;

            string[] parts = command.Trim().ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < parts.Length; i += 2)
            {
                switch(parts[i])
                {
                    case "at":
                        if(parts[i + 1].Length == 1)
                            fuzzy = true;
                        if(!int.TryParse(parts[i + 1], out target))
                            yield break;
                        break;
                    case "hold":
                        if(!int.TryParse(parts[i + 1], out duration))
                            yield break;
                        break;
                    default:
                        yield break;
                }
            }

            yield return null;

            while(target != -1 && (fuzzy ? (Info.GetTime() % 10 < target + 0.2f || Info.GetTime() % 10 > target + 0.8f) : (Info.GetTime() % 60 < target + 0.2f || Info.GetTime() % 60 > target + 0.8f)))
                yield return "trycancel The button was not pressed due to a request to cancel.";
            float h = Info.GetTime();
            Button.OnInteract();
            while(Mathf.Abs((int)h - (int)Info.GetTime()) < duration)
                yield return null;
            Button.OnInteractEnded();

            if(_expectedActions.Count == 0)
                yield return "solve";
        }
    }
}