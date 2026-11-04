# NIGHT TRAIN ZERO: quick start (full game)

1. Let Unity compile.
2. **Tools → Subnet → Build Full Game**. It generates all 6 scenes, wires everything and adds them to the build list.
3. Open `Assets/_Project/Scenes/00_MainMenu` (it opens automatically) and press **Play**.

**Story path:** apartment (check the handheld on box 14, then leave by the front door) → Central Station (read the timetable, board Night Train Zero) → Subnet (meet Byte, talk to Kira, hack the Wall of Overtime) → Concrete Park (light the lamps in the order the plaques describe, hack the Weeping Commuter) → Central Core (face The Algorithm, read the corrupted memory, choose an ending).

**Controls:** WASD/arrows move · Z/E/Enter/Space interact · X/Esc back · hold X to skip cutscenes. In Obstacle Hacking, move the diamond with WASD and pick memory fragments with up/down + Z.

**No sound?** The FMOD message "failed to switch back to normal output" means Unity lost your Mac's audio device. Check the Game view's mute button, choose an output in macOS Sound settings, then restart Unity. The game also re-initializes audio when the device changes.

---

# NIGHT TRAIN ZERO — Step 1 Setup Guide

Unity 6000.3 · URP 17.3 · Input System **only** (Active Input Handling = "Input System Package (New)") · TextMeshPro (in uGUI 2.0)

All scripts live under `Assets/_Project/Scripts` in the `Subnet.*` namespaces. No third-party packages are needed: tweening is done in code (`Subnet.Core.Easing`), not DOTween.

---

## 0. First run (2 minutes)

1. Let Unity compile. If it asks, import **TMP Essential Resources** (Window → TextMeshPro → Import TMP Essential Resources).
2. Run **Tools → Subnet → Setup Project Structure**. It creates the folders below, plus:
   - **Tags:** `Inspectable`, `NPC`, `CinematicPoint`, `Obstacle` (`Player` is built in)
   - **Layers:** 8 `Player`, 9 `Interactable`, 10 `Ground`, 11 `Obstacle`, 12 `NPC`
3. *(Recommended)* **Inside the Unity Project window**, rename `Assets/Assets` to `Assets/ThirdParty/Kenney`. Always move assets inside Unity, never in Finder, so the references (`.meta` GUIDs) stay intact.
4. Create the scenes in `Assets/_Project/Scenes`: `00_MainMenu`, `01_Tenement`. Add both to **File → Build Profiles → Scene List**, with `00_MainMenu` first.
5. Select the EventSystem in every scene. If it shows *Standalone Input Module*, click **"Replace with InputSystemUIInputModule"**.

## 1. Folder architecture

```
Assets/
├─ _Project/
│  ├─ Animation/{Controllers,Clips}
│  ├─ Art/{Materials/{Reality,Subnet},Shaders,Fonts,Sprites/{UI,Portraits},VFX}
│  ├─ Audio/{Mixers,Music,Ambience,SFX/{UI,Footsteps,Hacking}}
│  ├─ Data/{Dialogue/{Act1_Tenement,Act2_Commercial,Act3_ConcretePark,Act4_CentralCore},Speakers}
│  ├─ Editor/                     SubnetProjectSetup.cs
│  ├─ Prefabs/{Characters,Props/Inspectables,Environment,UI,Systems}
│  ├─ Scenes/                     00_MainMenu, 01_Tenement, 02_Station, 03_Subnet_Commercial, ...
│  ├─ Scripts/
│  │  ├─ Core/          GameInput, InputLock, Easing, GameSettings, SaveData, StoryFlags, ScreenFader
│  │  ├─ UI/            MainMenuController, RetroMenuButton
│  │  ├─ Cinematics/    CinematicDirector, CinematicSequence (+CinematicShot), CinematicTrigger
│  │  ├─ CameraSystem/  IsometricCameraRig
│  │  ├─ Player/        IsometricPlayerController, PlayerInteractor
│  │  ├─ Interaction/   IInteractable, InspectableObject
│  │  └─ Dialogue/      DialogueManager, DialogueSequence, DialogueSpeaker, DialogueChoiceButton
│  ├─ Settings/PostProcessing
│  └─ Timeline/
├─ ThirdParty/Kenney/   (the current Assets/Assets folder)
└─ Settings/            (URP assets from the template, leave as is)
```

## 2. Shared UI conventions

- Every Canvas: **Canvas Scaler → Scale With Screen Size, 1920×1080, Match 0.5**.
- Font: until a monospace pixel font is imported (see §8), use TMP's default *LiberationSans SDF*.
- Palette: Reality `#1B1E24` / `#8A9099`. Subnet neons: cyan `#33FFF2`, magenta `#FF4FD8`, pastel lilac `#B9A7FF`.

---

## 3. Scene `00_MainMenu`

```
00_MainMenu
├─ Main Camera              Camera (Post Processing ✔), clear Solid Color #07080B
├─ Global Volume            Volume (Global) → profile with Bloom 0.6, Vignette 0.3, Chromatic Aberration 0.12, Film Grain 0.2
├─ EventSystem              EventSystem + InputSystemUIInputModule
├─ Audio
│  ├─ UI_Source             AudioSource (2D, Play On Awake ✘)
│  ├─ Hum_Source            AudioSource (2D, Loop ✔, clip = subway hum)   ← see §8
│  └─ Rain_Source           AudioSource (2D, Loop ✔, clip = rain loop)    ← see §8
├─ MenuCanvas               Canvas: **Screen Space - Camera** (Main Camera, Plane Distance 1) so post FX hit the UI
│  └─ ScreenRoot            RectTransform (stretch), scaled by the power-off effect
│     ├─ Background         Image #0B0F14 (stretch). Later: apartment-window render texture
│     ├─ TopBar             TMP "LEO-OS" (left) + ClockText TMP (right)
│     ├─ BootLog            TMP, top-left aligned, size 26, color #33FFF2, overflow Truncate
│     ├─ MainPanel          CanvasGroup + Vertical Layout Group (spacing 18)
│     │  ├─ Btn_Boot        Button + RetroMenuButton (label child TMP, labelText "BOOT SYSTEM")
│     │  ├─ Btn_Memories    "CORRUPTED MEMORIES"
│     │  ├─ Btn_Config      "SYSTEM CONFIG"
│     │  └─ Btn_Shutdown    "SHUT DOWN"
│     ├─ MemoriesPanel      CanvasGroup
│     │  └─ List            Vertical Layout Group  (= memoriesListRoot)
│     │     ├─ Entry_Template   RetroMenuButton, **inactive** (= memoryEntryPrefab)
│     │     └─ Btn_Back         RetroMenuButton "RETURN"
│     ├─ ConfigPanel        CanvasGroup + Vertical Layout Group
│     │  ├─ Row_Master      TMP label + Slider (0–1) + TMP value text
│     │  ├─ Row_Music       ...
│     │  ├─ Row_SFX         ...
│     │  ├─ Toggle_Shake    Toggle "SCREEN SHAKE"
│     │  ├─ Toggle_Fullscreen
│     │  └─ Btn_Back        RetroMenuButton "SAVE AND RETURN"
│     ├─ StatusText         TMP bottom-left, size 22, color #8A9099
│     └─ CRT_Overlay        Image (stretch, Raycast Target ✘), Material = M_CRTOverlay
├─ FadeCanvas               Canvas Overlay, Sort Order 100
│  └─ Fader                 Image black (stretch) + CanvasGroup + ScreenFader (startAlpha 1)
└─ MainMenu                 MainMenuController (wire everything above)
```

- **M_CRTOverlay**: in `Art/Materials`, create a material using the shader **Subnet/UI/CRTOverlay**. Keep Tint = black, alpha 1.
- **Navigation:** leave the buttons on Automatic navigation, or set it to Explicit (vertical) if focus jumps around.
- **Chapters** (MainMenuController → Chapters):
  - `MEM_01 // THE MOVING BOXES` → `01_Tenement`, key empty
  - `MEM_02 // NIGHT TRAIN ZERO` → `02_Station`, key `act2`
  - `MEM_03 // CONCRETE PARK` → `04_ConcretePark`, key `act3`
  - `MEM_04 // CENTRAL CORE` → `05_CentralCore`, key `act4`
- **Audio mixer (optional but recommended):** `Audio/Mixers/MainMixer` with groups Music and SFX under Master. Expose each group's Volume as `MasterVolume`, `MusicVolume` and `SFXVolume`.

**Kenney Interface Sounds mapping** (`ThirdParty/Kenney/kenney_interface-sounds/Audio/`)

| Field | Clip |
|---|---|
| hoverClip | `tick_001` |
| confirmClip | `confirmation_002` |
| backClip | `back_002` |
| errorClip | `error_006` |
| openPanelClip | `maximize_008` |
| closePanelClip | `minimize_008` |
| toggleClip | `switch_002` |
| sliderTickClip | `tick_002` |
| bootBlipClip | `click_002` |
| powerOffClip | `minimize_005` |
| glitchClips | `glitch_001` … `glitch_004` |

---

## 4. Scene `01_Tenement`: gameplay core

```
01_Tenement
├─ Main Camera              Camera FOV 26, Post Processing ✔ + IsometricCameraRig (target = Leo, pitch 35, yaw 45, distance 16)
├─ Directional Light        cold #8FA3C7, intensity 0.35, soft shadows
├─ Global Volume            Reality profile: Saturation -35, Vignette 0.35, Bloom 0.3, Film Grain 0.25
├─ EventSystem              InputSystemUIInputModule
├─ Environment              floor/walls (layer Ground), props (see §6)
├─ Leo                      Tag Player, Layer Player
│  │  CharacterController (Height 1.0, Radius 0.25, Center (0,0.5,0), Step Offset 0.25)
│  │  IsometricPlayerController
│  │  PlayerInteractor (Interactable Mask = Interactable, prompt = Prompt below)
│  │  AudioSource (2D)
│  └─ Model                 kenney_mini-characters/character-male-b.fbx (+ Animator with float "Speed")
├─ CinematicPoints          empties tagged CinematicPoint: CP_01_Ceiling, CP_02_Boxes, CP_03_Handheld
├─ Cinematic_Intro          CinematicSequence (see §5)
├─ CinematicDirector        CinematicDirector (camera, rig, fader, bars, intro, Play Intro On Start ✔)
│                           + 2 AudioSources: Ambience (Loop ✔) and SFX
└─ UI
   ├─ HUDCanvas (Overlay, order 0)
   │  └─ Prompt             Image (Kenney input-prompts "Keyboard & Mouse" E/Z key), 48×48, anchors center
   ├─ DialogueCanvas (Overlay, order 10)
   │  └─ DialogueRoot       CanvasGroup (= rootGroup) + DialogueManager + 2 AudioSources (Voice, UI)
   │     └─ Box             Image #0B0F14 α .92 with a 3px #33FFF2 border sprite, anchors bottom-stretch, height 260, pivot (0.5,0)
   │        ├─ Portrait     Image 200×200, left
   │        ├─ NamePlate    Image + NameText (TMP 28, bold)
   │        ├─ BodyText     TMP 34, Wrapping ✔, Rich Text ✔, line spacing 10
   │        ├─ AdvanceIndicator  TMP "v" or a Kenney arrow sprite (LiberationSans has no ▼ glyph), bottom-right
   │        └─ Choices      Vertical Layout Group (Child Force Expand ✘), right side
   ├─ CinematicCanvas (Overlay, order 50)
   │  ├─ LetterboxTop       Image black · anchors (0,1)-(1,1) · pivot (0.5,1) · height 0
   │  ├─ LetterboxBottom    Image black · anchors (0,0)-(1,0) · pivot (0.5,0) · height 0
   │  └─ SkipHint           CanvasGroup α0 → TMP "HOLD [X] TO SKIP" + Image (Filled, Radial 360) as progress
   └─ FadeCanvas (Overlay, order 100) → Fader (ScreenFader, startAlpha 1)
```

**Choice prefab** (`Prefabs/UI/DialogueChoice.prefab`): RectTransform 520×56 + **DialogueChoiceButton** + Image (alpha 0, Raycast ✔ so hover works). Children: `Cursor` (TMP ">" magenta, or a heart sprite) and `Label` (TMP 30).

**DialogueManager audio:** open `open_001`, close `close_001`, advance `click_001`, choice move `tick_001`, choice confirm `confirmation_001`.

**Animator for Kenney mini-characters:** these models ship with their own animation clips. Create `Animation/Controllers/AC_Leo` with a 1D Blend Tree on `Speed` (0 = idle clip, 1 = walk clip). If the FBX has no clips, tell me and I'll set up a procedural bob instead.

---

## 5. Intro cutscene: "The Moving Boxes" (`Cinematic_Intro`)

| Field | Value |
|---|---|
| fadeFromBlack | ✔, black hold 2.0, fade in 4.0 |
| useLetterbox | ✔ |
| blendToGameplay | ✔, 2.5 s |
| ambienceLoop | station rumble + rain (see §8) |

| # | Point | Travel | Hold | FOV | Narration (Leo, no speaker) |
|---|---|---|---|---|---|
| 1 | CP_01_Ceiling: high, looking down at the dark room | cut | 1.5 | 30 | *(startSound: distant train horn)* |
| 2 | CP_02_Boxes: low glide across the half-unpacked boxes | 6 s SmootherStep | 0.5 | 24 | "Fourteen boxes. I packed my whole life into fourteen boxes." / "Somehow it still feels like something's missing." |
| 3 | CP_03_Handheld: close on the glowing handheld on the box | 4 s SmootherStep | 1.0 | 18 | "The only thing still turned on in this apartment is my handheld." / "...and me. Barely." |

When the blend finishes, control returns automatically.

---

## 6. Act I inspectables (Kenney furniture-kit → `InspectableObject`)

Put each prop on layer **Interactable**, tag **Inspectable**, with a BoxCollider. Leave Speaker empty (Leo's inner voice).

| Prop (furniture-kit) | First inspection | Repeat |
|---|---|---|
| `cardboardBoxOpen` | "A half-unpacked box. Everything inside is wrapped in newspaper from a city you don't live in anymore." | "You'll unpack it tomorrow. You said that yesterday." |
| `cardboardBoxClosed` | "This one's still taped shut. You wrote 'MISC' on it, which is what you write when you don't want to remember." | "MISC. Miscellaneous. Mis-sell-a-nee-us. The word stops meaning anything." |
| `bathroomMirror` (cracked) | "A cracked mirror. The crack runs right through your reflection, like it's trying to split you into a before and an after." / "You look away first." | "Still cracked. Still you. Both of those feel like problems." |
| `kitchenCoffeeMachine` | "The coffee machine hums like it's the only one here with a schedule." | "It's judging you. Politely. In espresso." |
| `kitchenFridgeSmall` | "Inside: one lemon, a jar of something, and the cold, clean light of good intentions." | "The lemon hasn't moved. Neither have you." |
| `bedSingle` | "An unmade bed. The sheets still have the shape of a night you didn't sleep through." | "Not yet. If you lie down now, the ceiling starts talking." |
| `laptop` | "Your laptop. Seventeen unread emails. The subject lines all sound like sighs." | "Closing the lid doesn't make them go away. But it helps." |
| `televisionVintage` | "An old TV the last tenant left behind. The static sounds like a crowd whispering your name, but slightly wrong." | "Lo. Leeo. Leeeo. ...It's just static. Probably." |
| `lampWall` (flickering) | "A flickering lamp. It's trying its best. You know the feeling." | "Flicker. Flicker. Hang in there, buddy." |
| `trashcan` | "Takeout boxes and a crumpled train ticket. Destination: home. Departure date: three weeks ago." | "You never used that ticket." |
| `plantSmall1` | "A plant from the corner shop. The tag says 'impossible to kill.' It's taking that as a challenge." | "You water it. It doesn't thank you. Fair." |
| `wallWindow` | "Central Station glows below, wet and neon. Trains come and go like thoughts you can't hold onto." | "One of the trains has no number. You blink. It's gone." |
| `radio` | "A radio stuck between stations. A weather report, a love song, and a sound like breathing, all at once." | "The breathing is still there." |
| `doorwayFront` | "The front door. The building's stairwell smells like rain and other people's dinners." *(sets `door_inspected`)* | "The station is five minutes away. Your handheld buzzes, like it's agreeing." |
| `rugDoormat` | "A doormat that says WELCOME. It came with the apartment. It doesn't mean you." | "WELCOME. It's trying." |
| `books` | "Books you meant to read in your old life. They made the move. You're not sure you did." | "Chapter one, again." |

---

## 7. Speakers (`Data/Speakers`, Create → Subnet → Dialogue Speaker)

| Asset | displayName | voiceBlip | pitchRange | speed | blip every |
|---|---|---|---|---|---|
| Speaker_LeoInner (DialogueManager default) | *(empty)* | `tick_002` | 0.92–1.0 | 1.0 | 2 |
| Speaker_Leo | LEO | `click_002` | 0.95–1.05 | 1.0 | 2 |
| Speaker_Byte | BYTE | `click_004` | 1.35–1.55 | 1.25 | 2 |
| Speaker_Kira | KIRA | `pluck_002` | 1.1–1.25 | 1.2 | 2 |
| Speaker_Algorithm | THE ALGORITHM | `glitch_002` | 0.5–0.6 | 0.6 | 3 |
| Speaker_Object | *(empty, e.g. talking streetlamp)* | `drop_002` | 0.8–0.9 | 0.85 | 3 |

---

## 8. Assets still needed

The Kenney kits in the project have no ambient loops, no footstep sounds and no monospace font. To match the brief, please import:

| # | Need | Format | Free suggestion |
|---|---|---|---|
| 1 | Monospace / pixel font for LEO-OS UI and dialogue | `.ttf` | Kenney **Fonts** pack (Kenney Mini Square Mono) or **VT323** (Google Fonts, OFL) |
| 2 | Subway hum / station rumble loop (30–60 s, seamless) | `.ogg`/`.wav` 44.1 kHz | freesound.org (CC0 "subway ambience") |
| 3 | City rain loop (seamless) | `.ogg`/`.wav` | freesound.org CC0 |
| 4 | Distant train horn + subway door hiss (one-shots) | `.ogg`/`.wav` | freesound.org CC0 |
| 5 | Footsteps: asphalt, metal grating, wooden floor (4–5 variations each) | `.ogg` | Kenney **Impact Sounds** (footstep_concrete / wood / grass…) |
| 6 | 1–2 music loops: lo-fi melancholic (reality), pastel synthwave (Subnet) | `.ogg` | any CC0/CC-BY source |
| 7 | Paperwork monster / floating documents and clocks | `.fbx`/`.glb`, low-poly, flat colors | Quaternius or Kenney (no paper props in current kits); otherwise I'll build them procedurally from quads |
| 8 | Window/border 9-slice sprite for the dialogue box | `.png` | Kenney **UI Pack** (the pixel-adventure pack has tiles only, no thin tech borders) |

Items 1–5 matter for Step 1. Items 6–8 can wait until Step 2–3.
