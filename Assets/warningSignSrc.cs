﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

public class warningSignSrc : MonoBehaviour {

    public KMAudio Audio;
    public KMBombInfo bomb;

    public KMSelectable sign;
    public KMSelectable[] screws;
    public Sprite[] signSprites;
    public Sprite smileySprite;

    private int checkTime; //time when a screw is clicked
    private int firstPressTime; // first digit of the timer, when the first screw was pressed 

    private int chosenSign;
    private int stage = 0; //0-2 increase after klicking a screw
    private int[] numberTriangle = new int[10] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9}; //0=top; 1,2=firstrow; 3,4,5=secondrow; 6,7,8,9=bottomrow => this triangle gets shifted by the first digit of the serial number.

    private int[,] triangleLookup = new int[10, 3] { {4, 7, 8}, {5, 7, 9}, {3, 6, 8}, {2, 8, 9}, {0, 6, 9}, {1, 6, 7}, {2, 4, 5}, {0, 1, 5}, {0, 2, 3}, {1, 3, 4} };
    /*
    0 x x x
    1 2 x x
    3 4 5 x
    6 7 8 9
    lookup finds the three numbers, that are not part of any triangle constructed from the current position, e.g. Pos_2 is part of the triangles: (012) (245) (279), therefor not part are: [3,6,8]
    */
    private int firstDigitSerial;
    //these are the values for each column
    private int[] valueD = new int[20] {1, 6, 3, 7, 9, 2, 3, 0, 7, 6, 6, 2, 0, 8, 0, 1, 9, 1, 8, 8};
    private int[] valueA = new int[20] {8, 7, 0, 2, 5, 5, 4, 1, 8, 4, 8, 7, 2, 6, 4, 4, 1, 5, 9, 6};
    private int[] valueN = new int[20] {3, 2, 7, 5, 4, 3, 9, 9, 1, 7, 1, 4, 3, 9, 8, 0, 5, 9, 2, 1};
    private int[] valueG = new int[20] {7, 8, 6, 3, 0, 4, 5, 7, 3, 2, 5, 0, 6, 2, 5, 8, 6, 4, 3, 9};

    private int[,] solution = new int[4,3]; //[x,y] x = 0->D, 1->A, 2->N, 3->G / y = three possible valid answers
    private int solutionIndex; // 0 for D, 1 for A, 2 for N, 3 for G
    private string[] columnLetters = new string[4] { "D", "A", "N", "G" };
    private string[] stageLog = new string[3] { "First", "Second", "Third" };

    private int[] alreadyPressedNumbers = new int[2] {-1, -1}; // saves the values on wich a screw was pressed in stage 1 [0] and stage 2 [1] (nothing pressed is represented by -1)
    private KMSelectable[] alreadyPressedScrews = new KMSelectable[2]; // saves the screw selectables that were pressed in stage 1 [0] and 2 [1]

    private string[] signNames = new string[20] {
        "General warning",
        "Flammable material",
        "Radioactive material",
        "Toxic material",
        "High temperature",
        "Low temperature",
        "Mind your head",
        "High voltage",
        "Irritant",
        "Explosive substance",
        "Biohazard",
        "Laser beam",
        "Hazardous to the environment",
        "Corrosive substance",
        "Rotating parts",
        "Deep water",
        "Risk of falling",
        "Cameras",
        "Falling objects",
        "Slippery floor"
    };

    //constants for screw animation
    private const float smooth = 75f;
    private const float animationTime = 0.2f;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    private bool inputStarted = false; //becomes true on pressing the first screw

    void Awake()
    {
        moduleId = moduleIdCounter++;
        //disable the sign until the lights turn on
        sign.gameObject.SetActive(false);

        foreach (KMSelectable screw in screws)
        {
            KMSelectable pressedScrew = screw;
            screw.OnInteract += delegate () { ScrewPress(pressedScrew); return false; };
        }
        
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void OnActivate()
    {
        sign.gameObject.SetActive(true);
    }

    void Update()
    {
        if (moduleSolved || !inputStarted) { return; }

        int currentTime = (int)bomb.GetTime();
        if (firstPressTime >= currentTime + 10 || firstPressTime <= currentTime - 10)
        {
            Debug.LogFormat("[Warning Signs #{0}] 10 Seconds (in bomb time) have passed since first interaciton. Strike!", moduleId);
            StartCoroutine(Strike());
        }
    }

    // Use this for initialization
    void Start()
    {
        findSolution();
    }

    void findSolution()
    {
        Debug.LogFormat("[Warning Signs #{0}] Number of modules on the bomb: {1}", moduleId, bomb.GetModuleNames().Count());
        firstDigitSerial = bomb.GetSerialNumberNumbers().First();
        Debug.LogFormat("[Warning Signs #{0}] The first digit of the serial is: {1}", moduleId, firstDigitSerial);
        chosenSign = UnityEngine.Random.Range(0, 20); //choose one of the 20 signs to display.
        sign.GetComponent<SpriteRenderer>().sprite = signSprites[chosenSign];
        Debug.LogFormat("[Warning Signs #{0}] The sign on display is: {1}", moduleId, signNames[chosenSign]);
        // numberTriangle values shift backward (via +firstDigitSerial mod10) and triangleLookup index shifts backwards (via -firstDigitSerial Mod10)
        for (int i = 0; i < 3; i++)
        {
            solution[0, i] = (numberTriangle[triangleLookup[Mod((valueD[chosenSign] - firstDigitSerial), 10), i]] + firstDigitSerial) % 10;
            solution[1, i] = (numberTriangle[triangleLookup[Mod((valueA[chosenSign] - firstDigitSerial), 10), i]] + firstDigitSerial) % 10;
            solution[2, i] = (numberTriangle[triangleLookup[Mod((valueN[chosenSign] - firstDigitSerial), 10), i]] + firstDigitSerial) % 10;
            solution[3, i] = (numberTriangle[triangleLookup[Mod((valueG[chosenSign] - firstDigitSerial), 10), i]] + firstDigitSerial) % 10;
        }

        Debug.LogFormat("[Warning Signs #{0}] The constructed triangle is:", moduleId);
        Debug.LogFormat("[Warning Signs #{0}]       {1}", moduleId, firstDigitSerial);
        Debug.LogFormat("[Warning Signs #{0}]     {1}  {2}", moduleId, (firstDigitSerial+1)%10, (firstDigitSerial+2)%10);
        Debug.LogFormat("[Warning Signs #{0}]   {1}  {2}  {3}", moduleId, (firstDigitSerial+3)%10, (firstDigitSerial+4)%10, (firstDigitSerial+5)%10);
        Debug.LogFormat("[Warning Signs #{0}] {1}  {2}  {3}  {4}", moduleId, (firstDigitSerial+6)%10, (firstDigitSerial+7)%10, (firstDigitSerial+8)%10, (firstDigitSerial+9)%10);

        Debug.LogFormat("[Warning Signs #{0}] The digit for column D is: {1}. The solutions are: ({2}, {3}, {4}).",
            moduleId, valueD[chosenSign], solution[0, 0], solution[0, 1], solution[0, 2]);
        
        Debug.LogFormat("[Warning Signs #{0}] The digit for column A is: {1}. The solutions are: ({2}, {3}, {4}).",
            moduleId, valueA[chosenSign], solution[1, 0], solution[1, 1], solution[1, 2]);

        Debug.LogFormat("[Warning Signs #{0}] The digit for column N is: {1}. The solutions are: ({2}, {3}, {4}).",
            moduleId, valueN[chosenSign], solution[2, 0], solution[2, 1], solution[2, 2]);

        Debug.LogFormat("[Warning Signs #{0}] The digit for column G is: {1}. The solutions are: ({2}, {3}, {4}).",
            moduleId, valueG[chosenSign], solution[3, 0], solution[3, 1], solution[3, 2]);	
    }

    int Mod(int x, int m) // modulo function that always gives a positive value back
    {
        return (x % m + m) % m;
    }

    void ScrewPress(KMSelectable screw)
    {
        if (moduleSolved || screw == alreadyPressedScrews[0] || screw == alreadyPressedScrews[1]) {return;} //retrun if this screw has already been pressed.
        screw.AddInteractionPunch();

        checkTime = (int)bomb.GetTime() % 10; //get the last seconds digit of the countdown timer.
        int totalTime = (int)bomb.GetTime();
        int numberSolvedModules = bomb.GetSolvedModuleNames().Count;
        int numberOfUnsolvedModules = bomb.GetModuleNames().Count() - numberSolvedModules;
        solutionIndex = GetSolutionIndex();

        if (!inputStarted) // only log the following if this is the first input
        {
            Debug.LogFormat("[Warning Signs #{0}] There are {1} unsolved modules on the bomb, therfore use the digit in column {2}", moduleId, numberOfUnsolvedModules, columnLetters[solutionIndex]); 
        }
        Debug.LogFormat("[Warning Signs #{0}] Time of interaction is: {1}", moduleId, bomb.GetFormattedTime());
        //Debug.LogFormat("[Warning Signs #{0}] Time of interaction is: {1}", moduleId, checkTime);
        
        //check if a screw was clicked at one if the 3 valid times and start the timer.
        if (checkTime == solution[solutionIndex, 0] || checkTime == solution[solutionIndex, 1] || checkTime == solution[solutionIndex, 2])
        {
            if (stage == 0) //if this is the first stage start time keeping
            {
                firstPressTime = totalTime;
                inputStarted = true;
            }
            // if this is a later stage check if the number was already input.
            if (checkTime == alreadyPressedNumbers[0] || checkTime == alreadyPressedNumbers[1])
            {
                Debug.LogFormat("[Warning Signs #{0}] There has already been a screw unscrewd at this time.", moduleId, checkTime);
            }
            else if (stage == 2) //if this is the 3rd (last) stage
            {
                Debug.LogFormat("[Warning Signs #{0}] {1} unscrew at: {2}. Module solved!", moduleId, stageLog[stage], checkTime);
                StartCoroutine(ScrewOut(screw));
                GetComponent<KMBombModule>().HandlePass();
                StartCoroutine(SolveAnimation());
                moduleSolved = true;
            }
            else //if this is stage 1 or 2
            {
                Debug.LogFormat("[Warning Signs #{0}] {1} unscrew at: {2}. Correct! Proceeding to stage {3}.", moduleId, stageLog[stage], checkTime, stage+1);
                alreadyPressedNumbers[stage] = checkTime; //remeber that a screw was already pressed at this time
                alreadyPressedScrews[stage] = screw; // remeber that this screw was already pressed
                stage++; //proceed to the next stage
                StartCoroutine(ScrewOut(screw));
            }
        }
        else // if a screw was clicked at an invalid time
        {
            Debug.LogFormat("[Warning Signs #{0}] {1} unscrew at: {2}. The expected timer digits were: {3}, {4} and {5}. Strike!", moduleId, stageLog[stage], checkTime, solution[solutionIndex, 0], solution[solutionIndex, 1], solution[solutionIndex, 2]);
            StartCoroutine(Strike());
        }
    }

    IEnumerator Strike()
    {
        GetComponent<KMBombModule>().HandleStrike(); //give a strike
        Audio.PlaySoundAtTransform("screwdriver", transform);
        inputStarted = false; //reset input timeslot
        stage = 0; //set stage back to 0
        alreadyPressedNumbers[0] = -1; // reset the already pressed numbers
        alreadyPressedNumbers[1] = -1;
        if (alreadyPressedScrews[0] != null)
        {
            StartCoroutine(ScrewIn(alreadyPressedScrews[0]));
            alreadyPressedScrews[0] = null;
        }
        if (alreadyPressedScrews[1] != null)
        {
            StartCoroutine(ScrewIn(alreadyPressedScrews[1]));
            alreadyPressedScrews[1] = null;
        }
        yield return null;
    }

    int GetSolutionIndex()
    {
        int numberSolvedModules = bomb.GetSolvedModuleNames().Count;
        int numberOfUnsolvedModules = bomb.GetModuleNames().Count() - numberSolvedModules;

        if (numberOfUnsolvedModules % 4 == 0) //if the number of unsolved modules is divisible by four
        {
            return 0; //use the value in manual column D
        }
        else if (numberOfUnsolvedModules % 3 == 0) //if the number of unsolved modules is divisible by three
        {
            return 1; //use the value in manual column A
        }
        else if (numberOfUnsolvedModules % 2 == 0) //if the number of unsolved modules is divisible by two and not by four
        {
            return 2; //use the value in manual column N
        }
        else //otherwise
        {
            return 3; //use the value in manual column G
        }
    }

    IEnumerator ScrewOut(KMSelectable screw)
    {
        Audio.PlaySoundAtTransform("squeak", transform);
        while(screw.transform.localPosition.y < 0.03f)
        {
            screw.transform.localPosition += 0.05f * Time.deltaTime * Vector3.up;
            screw.gameObject.transform.Rotate(Vector3.up, - 500 * Time.deltaTime);
            yield return new WaitForSeconds(0.002f);
        }
        screw.gameObject.SetActive(false);
        yield return null;
    }

    IEnumerator ScrewIn(KMSelectable screw)
    {
        screw.gameObject.SetActive(true);
        while(screw.transform.localPosition.y > 0.005f)
        {
            screw.transform.localPosition += 0.05f * Time.deltaTime * Vector3.down;
            screw.gameObject.transform.Rotate(Vector3.up, + 500 * Time.deltaTime);
            yield return new WaitForSeconds(0.002f);
        }
        yield return null;
    }

    IEnumerator SolveAnimation()
    {
        Audio.PlaySoundAtTransform("success", transform);
        sign.GetComponent<SpriteRenderer>().sprite = smileySprite;
        while (sign.transform.localPosition.z > -0.027f)
        {
            sign.transform.localPosition += 0.05f * Time.deltaTime * Vector3.back;
            sign.gameObject.transform.Rotate(Vector3.back, + 5 * Time.deltaTime);
            yield return new WaitForSeconds(0.002f);
        }
        yield return null;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} unscrew 1 3 6> to unscrew the screws at those timer digits. The order is irrelevant.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToUpperInvariant();
		Match m = Regex.Match(command, @"^UNSCREW\s+((?:[0-9]\s*){3})$");
		if (m.Success)
        {
			yield return null;
			List<int> submission = m.Groups[1].Value.Where(x => x != ' ').Select(x => x - '0').ToList();
			while (submission.Contains((int)bomb.GetTime() % 10))
				yield return "trycancel"; //This is needed or else there can be a bug where it presses at the wrong time.
			while (!submission.Contains((int)bomb.GetTime() % 10))
				yield return "trycancel";
            for (int i = 0; i < 3; i++)
            {
				yield return new WaitForSeconds(0.05f);
				screws[i].OnInteract();
				submission.Remove((int)bomb.GetTime() % 10);
				while (!submission.Contains((int)bomb.GetTime() % 10))
					yield return null;
            }
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        int currentScrew = 0;
        List<int> submission;
        do
        {
            submission = new List<int> { solution[GetSolutionIndex(), 0], solution[GetSolutionIndex(), 1], solution[GetSolutionIndex(), 2] };
            yield return true;
        } while (!submission.Contains((int)bomb.GetTime() % 10)); 
        submission = new List<int> { solution[GetSolutionIndex(), 0], solution[GetSolutionIndex(), 1], solution[GetSolutionIndex(), 2] };
        while (submission.Count > 0)
        {
            while (!submission.Contains((int)bomb.GetTime() % 10))
                yield return null;
            submission.Remove((int)bomb.GetTime() % 10);
            screws[currentScrew].OnInteract();
            yield return new WaitForSeconds(0.3f);
            currentScrew++;
        }
    }
}
