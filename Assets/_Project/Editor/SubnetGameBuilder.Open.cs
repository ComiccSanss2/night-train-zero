#if UNITY_EDITOR
using Subnet.Audio;
using Subnet.Dialogue;
using Subnet.Gameplay;
using Subnet.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>
    /// Places off the main road.
    ///   Subnet, south of the street → the Night Market (opens once the Wall of Overtime is gone):
    ///       a noodle cart (side quest: a cup with a cloud), a ringing payphone (gives the garden key), a fortune stall.
    ///   Concrete Park, south side   → the Quiet Garden behind a locked gate (needs the payphone's key):
    ///       a koi pond, paper boats, a bench to sit on with Byte.
    /// Nothing here is needed to finish the story; it's there for whoever wanders off.
    /// </summary>
    public static partial class SubnetGameBuilder
    {
        // ====================================================================== shared bits

        private static DialogueSpeaker spkCook, spkSeer;

        private static DialogueSpeaker Cook() => spkCook != null ? spkCook : (spkCook = CreateSpeaker("Speaker_Cook", "NOODLE GUY", "pluck_002",
            new Vector2(0.85f, 0.95f), 1.05f, new Color(1f, 0.55f, 0.35f), SpeakerStyle.Default, new Color(1f, 0.9f, 0.82f)));

        private static DialogueSpeaker Seer() => spkSeer != null ? spkSeer : (spkSeer = CreateSpeaker("Speaker_Seer", "MADAME TOKEN", "pluck_001",
            new Vector2(1.05f, 1.15f), 0.9f, new Color(0.75f, 0.55f, 1f), SpeakerStyle.Default, new Color(0.92f, 0.86f, 1f)));

        private static ParticleSystem Steam(Transform parent, Vector3 position, Material material, float rate = 6f)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.2f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 1f, 1f, 0.45f);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            return ps;
        }

        /// <summary>Gently drifting particles (petals, fireflies) over an area.</summary>
        private static ParticleSystem Drift(Transform parent, Vector3 center, Vector3 size, Material material, float rate, float particleSize, float fall)
        {
            var go = new GameObject("Drift");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.6f, particleSize);
            main.gravityModifier = fall;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        /// <summary>A world-aligned box collider around (part of) a model, on its own child object.</summary>
        private static GameObject Hitbox(string name, Transform parent, Vector3 center, Vector3 size, int layer, bool solid = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.layer = layer;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = !solid;
            return go;
        }

        private static Light Pulse(Light light, LightPulse.Mode mode, float speed, float depth, string stopFlag = "")
        {
            if (light == null) return null;
            var p = light.gameObject.AddComponent<LightPulse>();
            p.mode = mode;
            p.speed = speed;
            p.depth = depth;
            p.stopFlag = stopFlag;
            return light;
        }

        /// <summary>A string of little lantern bulbs hung between two points (with a slight sag).</summary>
        private static void LanternString(Transform parent, Vector3 a, Vector3 b, int count, Material[] bulbs, Material cord)
        {
            Vector3 prev = a;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.35f);
                if (i > 0)
                {
                    Vector3 mid = (prev + p) * 0.5f;
                    GameObject seg = Deco("Cord", parent, mid, new Vector3(0.015f, 0.015f, Vector3.Distance(prev, p)), cord);
                    seg.transform.rotation = Quaternion.LookRotation(p - prev);
                }
                if (i > 0 && i < count)
                {
                    GameObject bulb = Deco("Bulb", parent, p + Vector3.down * 0.08f, new Vector3(0.11f, 0.14f, 0.11f), bulbs[i % bulbs.Length]);
                    bulb.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                }
                prev = p;
            }
        }

        // ====================================================================== Subnet: the Night Market

        private static void BuildNightMarket(Ctx ctx, Material ground, Material neonCyan, Material neonMagenta, Material poleMat)
        {
            const string F = "furniture";
            // Plaza x 3.5..14.5, z -4.3..-11. The street's south boundary has a gap at x 7.5..10.5.
            Block("Bound_South_W", ctx.env, new Vector3(-5.25f, 1f, -4.3f), new Vector3(25.5f, 2f, 0.2f), null, 0);
            Block("Bound_South_E", ctx.env, new Vector3(17.25f, 1f, -4.3f), new Vector3(13.5f, 2f, 0.2f), null, 0);
            Material tiles = LitMaterial("Subnet/M_MarketTiles", new Color(0.33f, 0.27f, 0.42f), 0.35f);
            Block("MarketGround", ctx.env, new Vector3(9f, -0.1f, -7.6f), new Vector3(11f, 0.2f, 7f), tiles, ctx.groundLayer);
            Block("Market_BoundS", ctx.env, new Vector3(9f, 1f, -11.1f), new Vector3(11.4f, 2f, 0.2f), null, 0);
            Block("Market_BoundW", ctx.env, new Vector3(3.4f, 1f, -7.6f), new Vector3(0.2f, 2f, 7f), null, 0);
            Block("Market_BoundE", ctx.env, new Vector3(14.6f, 1f, -7.6f), new Vector3(0.2f, 2f, 7f), null, 0);
            // Low edge so the plaza reads as a place, not a hole in the map.
            Material curb = LitMaterial("Subnet/M_MarketCurb", new Color(0.2f, 0.16f, 0.26f));
            Deco("MarketCurbS", ctx.env, new Vector3(9f, 0.1f, -11.05f), new Vector3(11.2f, 0.2f, 0.12f), curb);
            Deco("MarketCurbW", ctx.env, new Vector3(3.45f, 0.1f, -7.6f), new Vector3(0.12f, 0.2f, 7f), curb);
            Deco("MarketCurbE", ctx.env, new Vector3(14.55f, 0.1f, -7.6f), new Vector3(0.12f, 0.2f, 7f), curb);
            for (float x = 4.5f; x < 14f; x += 1.6f)
                Deco("MarketTileLine", ctx.env, new Vector3(x, 0.004f, -7.6f), new Vector3(0.03f, 0.01f, 6.8f), curb);
            AddStemZone(ctx, "NightMarket", new Vector3(9f, 1f, -7.6f), new Vector3(11f, 4f, 7f), StemMix.Quiet);

            // The shutter: rolls up once the overtime is over.
            Material shutterMat = LitMaterial("Subnet/M_Shutter", new Color(0.45f, 0.42f, 0.5f), 0.5f);
            GameObject shutter = Hitbox("MarketShutter", ctx.props, new Vector3(9f, 1.1f, -4.3f), new Vector3(3f, 2.2f, 0.12f), ctx.interactableLayer);
            Deco("ShutterPanel", shutter.transform, new Vector3(9f, 1.1f, -4.3f), new Vector3(3f, 2.2f, 0.12f), shutterMat);
            for (int i = 0; i < 7; i++)
                Deco("ShutterRib", shutter.transform, new Vector3(9f, 0.2f + i * 0.3f, -4.37f), new Vector3(3f, 0.04f, 0.03f), curb);
            TextMeshPro sTxt = WorldSign(shutter.transform, "NIGHT MARKET", new Vector3(9f, 1.55f, -4.38f), 180f, new Color(1f, 0.45f, 0.8f), 3f);
            sTxt.rectTransform.sizeDelta = new Vector2(2.6f, 0.4f);
            FitText(sTxt);
            TextMeshPro sSub = WorldSign(shutter.transform, "OPENS WHEN WORK ENDS", new Vector3(9f, 1.15f, -4.38f), 180f, new Color(0.85f, 0.85f, 0.95f), 1.6f);
            sSub.rectTransform.sizeDelta = new Vector2(2.4f, 0.22f);
            FitText(sSub);
            Inspectable(shutter, "MarketShutter",
                L("* NIGHT MARKET. OPENS WHEN WORK ENDS.", "* Behind it, somebody's frying something. Smells incredible."),
                L("* Still shut. The frying smell is getting personal."));
            var gate = shutter.AddComponent<FlagGate>();
            Set(gate, "openFlag", "hack1_done");
            Set(gate, "dissolveColor", new Color(1f, 0.6f, 0.85f));
            Event(ctx, "MarketOpens", StoryEvent.TriggerMode.OnFlag, "hack1_done", 9f, "BYTE",
                L("the market south of the street just opened. {p}smells like noodles. {p}{small}im not saying go. im saying noodles.{/small}"), null, null, string.Empty);
            Event(ctx, "EnterMarket", StoryEvent.TriggerMode.OnPlayerEnter, string.Empty, 0f, null, null, null, null, "visited_market",
                new Vector3(9f, 1f, -5.6f), new Vector3(3f, 2f, 1.2f));

            // Arch over the entrance.
            foreach (float x in new[] { 7.3f, 10.7f })
                Deco("ArchPost", ctx.props, new Vector3(x, 1.5f, -4.6f), new Vector3(0.14f, 3f, 0.14f), poleMat);
            Deco("ArchBeam", ctx.props, new Vector3(9f, 3f, -4.6f), new Vector3(3.6f, 0.12f, 0.14f), poleMat);
            Deco("ArchNeon", ctx.props, new Vector3(9f, 2.85f, -4.68f), new Vector3(3.2f, 0.05f, 0.04f), neonMagenta);

            // Lanterns strung across the plaza.
            Material[] bulbs =
            {
                EmissiveMaterial("Subnet/M_Bulb_Amber", new Color(0.5f, 0.3f, 0.1f), new Color(1f, 0.6f, 0.25f) * 3f),
                EmissiveMaterial("Subnet/M_Bulb_Pink", new Color(0.5f, 0.15f, 0.35f), new Color(1f, 0.35f, 0.75f) * 3f),
                EmissiveMaterial("Subnet/M_Bulb_Cyan", new Color(0.1f, 0.4f, 0.4f), Cyan * 2.5f),
            };
            Material cord = LitMaterial("Subnet/M_Cord", new Color(0.08f, 0.07f, 0.1f));
            Vector3[] posts = { new Vector3(14.3f, 0f, -4.7f), new Vector3(14.3f, 0f, -10.8f), new Vector3(3.7f, 0f, -10.8f) };
            foreach (Vector3 p in posts) Deco("LanternPole", ctx.props, p + Vector3.up * 1.6f, new Vector3(0.1f, 3.2f, 0.1f), poleMat);
            LanternString(ctx.props, new Vector3(10.7f, 3f, -4.6f), posts[1] + Vector3.up * 3.1f, 10, bulbs, cord);
            LanternString(ctx.props, new Vector3(7.3f, 3f, -4.6f), posts[2] + Vector3.up * 3.1f, 10, bulbs, cord);
            LanternString(ctx.props, posts[0] + Vector3.up * 3.1f, posts[1] + Vector3.up * 3.1f, 8, bulbs, cord);
            Pulse(PointLight(ctx.env, new Vector3(9f, 2.6f, -7.6f), new Color(1f, 0.62f, 0.4f), 2.6f, 9f), LightPulse.Mode.Flicker, 0.6f, 0.15f);
            PointLight(ctx.env, new Vector3(5.5f, 2.2f, -9.5f), Magenta, 1.4f, 6f);

            // ---------------------------------------------------------------- the noodle cart
            Vector3 cartPos = new Vector3(13.2f, 0f, -8.2f);
            GameObject cart = PropModel("Prop_NoodleCart", ctx.props, cartPos, 2.7f, -100f);
            if (cart == null) cart = Block("NoodleCartFallback", ctx.props, cartPos + Vector3.up * 0.6f, new Vector3(1f, 1.2f, 1.8f), LitMaterial("Subnet/M_CartRed", new Color(0.6f, 0.1f, 0.08f)), 0);
            Bounds cb = WorldBounds(cart);
            Hitbox("CartBody", cart.transform, new Vector3(cb.center.x, 0.8f, cb.center.z), new Vector3(Mathf.Min(cb.size.x, 1.3f), 1.6f, Mathf.Min(cb.size.z, 2.1f)), 0);
            Material steamMat = ParticleMaterial("Subnet/M_Steam", new Color(1f, 1f, 1f, 0.6f));
            Transform steamPoint = FindDeep(cart.transform, "SteamPoint");
            Steam(cart.transform, steamPoint != null ? steamPoint.position : cartPos + Vector3.up * 1.7f, steamMat);
            Transform signPoint = FindDeep(cart.transform, "SignPoint");
            if (signPoint != null)
            {
                Vector3 signFront = signPoint.position + (signPoint.position - new Vector3(cb.center.x, signPoint.position.y, cb.center.z)).normalized * 0.02f;
                float signYaw = Quaternion.LookRotation(new Vector3(signPoint.position.x - cb.center.x, 0f, signPoint.position.z - cb.center.z)).eulerAngles.y;
                TextMeshPro t = WorldSign(cart.transform, "NOODLES", signFront, signYaw, new Color(0.25f, 0.06f, 0.04f), 2.4f);
                t.rectTransform.sizeDelta = new Vector2(0.9f, 0.24f);
                FitText(t);
            }
            Pulse(PointLight(cart.transform, cartPos + new Vector3(-1f, 1.9f, 0f), new Color(1f, 0.55f, 0.25f), 2f, 4.5f), LightPulse.Mode.Flicker, 0.8f, 0.3f);
            for (int i = 0; i < 3; i++)
                Prop(ctx, "NoodleStool" + i, "stoolBar", F, cartPos + new Vector3(-1.25f, 0f, -0.75f + i * 0.75f), 0f, 0.62f, false, null);

            GameObject cook = Character(ctx, "NoodleCook", "character-male-d", "mini-characters", cartPos + new Vector3(-0.1f, 0f, 1.55f), 225f, 1.0f, null, true);
            DialogueSequence cookSeq = SeqAsset("Market/NoodleCook",
                OnlyIf("noodle_done", Say(Cook(), "Back for seconds? {p}Pull up a stool. {small}The left one wobbles. It's the best one.{/small}", "", DialogueSequence.EndNodeId)),
                OnlyIf("key.cloud_cup", Say(Cook(), "Oh, that's the one. Look at that little cloud.")),
                OnlyIf("key.cloud_cup", Say(Cook(), "She draws one on every cup. Every customer, every day. {p}{small}Nobody ever looks.{/small}")),
                OnlyIf("key.cloud_cup", Say(Cook(), "Here. Two bowls, on the house. {p}And a little something for walking all that way.", "", DialogueSequence.EndNodeId,
                    "noodle_done;take:cloud_cup;give:ramen*2#noodle_bowls;$6#noodle_tokens;+openness#noodle_done")),
                OnlyIf("noodle_quest", Say(Cook(), "No cup, no noodles. I don't make the rules. {p}{small}I do make the rules. But still.{/small}", "", DialogueSequence.EndNodeId)),
                Say(Cook(), "Kitchen's open! {p}Problem is, I ran out of cups an hour ago."),
                Say(Cook(), "The café up on the street throws out the good ones. The ones with the little clouds on. {p}Bring me one and I'll fill it.", "", "", "noodle_quest"));
            Inspectable(cook, "NoodleCook", L("..."), null, null, Cook(), cookSeq);

            // The cup itself, on the café table up on the street.
            GameObject cup = Prop(ctx, "CloudCup", "cup-coffee", "food", new Vector3(-2.85f, 0.71f, 2.45f), 30f, 0.16f, false,
                L("..."), null, null, false);
            {
                DialogueSequence cupSeq = SeqAsset("Market/CloudCup",
                    OnlyIf("!noodle_quest", Say(null, "A paper cup with a little cloud drawn on it. {p}Somebody took their time.", "", DialogueSequence.EndNodeId)),
                    Say(null, "A cup with a little cloud. That's the one he meant.", "", "", "key:cloud_cup;cup_found"));
                Inspectable(cup, "CloudCup", L("..."), null, null, null, cupSeq);
                Set(cup.AddComponent<HideIfFlag>(), "flag", "cup_found");
            }

            // ---------------------------------------------------------------- the payphone
            Vector3 phonePos = new Vector3(12.6f, 0f, -5.05f);
            GameObject phone = PropModel("Prop_Payphone", ctx.props, phonePos, 2.4f, 200f);
            if (phone == null) phone = Block("PayphoneFallback", ctx.props, phonePos + Vector3.up * 1.1f, new Vector3(0.9f, 2.2f, 0.6f), LitMaterial("Subnet/M_Teal", new Color(0.05f, 0.4f, 0.42f)), 0);
            Bounds pb = WorldBounds(phone);
            GameObject phoneHit = Hitbox("PayphoneBody", phone.transform, pb.center, new Vector3(Mathf.Min(pb.size.x, 1f), pb.size.y, Mathf.Min(pb.size.z, 1f)), ctx.interactableLayer);
            Transform lightPoint = FindDeep(phone.transform, "LightPoint");
            Pulse(PointLight(phone.transform, lightPoint != null ? lightPoint.position : phonePos + Vector3.up * 2f, new Color(1f, 0.2f, 0.25f), 1.6f, 3f),
                LightPulse.Mode.Blink, 2f, 1f, "booth_answered");
            DialogueSequence phoneSeq = SeqAsset("Market/Payphone",
                OnlyIf("booth_answered", Say(null, "Dial tone. {p}Whoever it was has hung up.", "", DialogueSequence.EndNodeId)),
                Say(null, "It's been ringing since you walked in. You pick up."),
                Say(spkUnknown, "{small}...hello? {p}Is this... sorry. I don't know whose number this is.{/small}"),
                Say(spkUnknown, "{small}I just keep calling it. Nobody ever picks up. {p}It's nice that somebody did.{/small}"),
                Say(spkUnknown, "{small}There's a garden in the park. Behind the wall nobody looks at. {p}I left the key under the phone. {slow}Like always.{/slow}{/small}"),
                Say(null, "Click. {p}Under the phone, taped to the shelf: a small brass key."),
                Say(spkLeoInner, "...Mia used to hide keys under things. {p}Doormats. Plant pots. {p}{slow}Phones.{/slow}", "", "", "booth_answered;key:garden_key"));
            Inspectable(phoneHit, "Payphone", L("..."), null, null, null, phoneSeq, phone.transform);

            // ---------------------------------------------------------------- fortune stall
            Vector3 stall = new Vector3(5.2f, 0f, -5.6f);
            Prop(ctx, "FortuneTable", "tableCloth", F, stall, 0f, 0.75f, false, null);
            Prop(ctx, "FortuneAwning", "detail-awning", "commercial", stall + new Vector3(0f, 1.9f, 0.45f), 180f, 1.6f, true, null, null, null, false);
            GameObject seer = Character(ctx, "FortuneTeller", "character-female-e", "mini-characters", stall + new Vector3(0f, 0f, 0.75f), 180f, 1.0f, null, true);
            Prop(ctx, "FortuneJar", "cup", "food", stall + new Vector3(0.25f, 0.8f, 0f), 0f, 0.2f, false, null, null, null, false);
            Material crystal = EmissiveMaterial("Subnet/M_Crystal", new Color(0.4f, 0.3f, 0.7f), new Color(0.7f, 0.5f, 1f) * 2f);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.name = "CrystalBall";
            ball.transform.SetParent(ctx.props, false);
            ball.transform.position = stall + new Vector3(-0.15f, 0.92f, 0f);
            ball.transform.localScale = Vector3.one * 0.24f;
            ball.GetComponent<Renderer>().sharedMaterial = crystal;
            Pulse(PointLight(ball.transform, ball.transform.position + Vector3.up * 0.2f, new Color(0.7f, 0.5f, 1f), 1.2f, 2.5f), LightPulse.Mode.Breathe, 0.4f, 0.5f);
            seer.layer = ctx.interactableLayer;
            var fortune = seer.AddComponent<TokenMachine>();
            Set(fortune, "machineId", "fortune");
            Set(fortune, "cost", 1);
            Set(fortune, "prompt", "Fortunes. One token. {p}{small}Refunds if they don't come true. Nobody's ever asked.{/small}");
            Set(fortune, "playOption", "[Pay  ◊1]");
            Set(fortune, "leaveOption", "[Not today]");
            SetStrings(fortune, "playingLines", L("She stares into the ball for a long time. {p}{small}Then she checks her phone.{/small}"));
            Set(fortune, "brokeLine", "No tokens? {p}Here's one for free: you'll find some tokens.");
            SetPrizes(fortune,
                ("", 30f, "You'll be late for something that doesn't matter. {p}And early for something that does."),
                ("", 25f, "Someone is thinking about you right now. {p}{small}It's me. You haven't paid the full amount.{/small}"),
                ("potion", 20f, "You'll feel better soon. {p}Here. That's not magic, it's a potion."),
                ("", 25f, "Call her. {p}{small}I don't know who. You do.{/small}"));
            Set(fortune, "guaranteedOnTry", 0);
            Set(fortune, "speaker", Seer());
            Set(fortune, "audioSource", ctx.sfx);
            Set(fortune, "playClip", Clip("switch_005"));
            Set(fortune, "winClip", Clip("confirmation_001"));

            // Skewer grill (smoke, coals, no talking: the grill speaks for itself).
            Vector3 grill = new Vector3(7.2f, 0f, -5.4f);
            Material coal = EmissiveMaterial("Subnet/M_Coals", new Color(0.2f, 0.03f, 0.02f), new Color(1f, 0.3f, 0.08f) * 2.5f);
            GameObject grillBox = Hitbox("Prop_Grill", ctx.props, grill + new Vector3(0f, 0.4f, 0f), new Vector3(1.1f, 0.8f, 0.5f), ctx.interactableLayer);
            Deco("GrillBody", grillBox.transform, grill + new Vector3(0f, 0.4f, 0f), new Vector3(1.1f, 0.8f, 0.5f), cord);
            Deco("GrillCoals", grillBox.transform, grill + new Vector3(0f, 0.81f, 0f), new Vector3(1f, 0.02f, 0.4f), coal);
            for (int i = 0; i < 6; i++)
                Prop(ctx, "Skewer" + i, "skewer", "food", grill + new Vector3(-0.4f + i * 0.16f, 0.84f, 0f), 90f, 0.4f, true, null, null, null, false);
            Steam(grillBox.transform, grill + new Vector3(0f, 0.95f, 0f), steamMat, 4f);
            Pulse(PointLight(grillBox.transform, grill + new Vector3(0f, 1.1f, -0.3f), new Color(1f, 0.4f, 0.15f), 1.4f, 3f), LightPulse.Mode.Flicker, 1.5f, 0.4f);
            Inspectable(grillBox, "Grill",
                L("Skewers of something. Grilled to perfection.", "* There's a sign: <color=#FFD84F>TAKE ONE. PAY LATER. OR DON'T. WE TRUST YOU.</color>"),
                L("* You take one. {p}Tastes like the first warm day of the year."), "give:potion#market_skewer");

            // Benches and a koi-less fountain-bowl of fairy lights (low things in the camera-side corner).
            GameObject b1 = Prop(ctx, "MarketBench", "bench", F, new Vector3(6.2f, 0f, -9.9f), 0f, 1.15f, true,
                L("* Nobody here is in a hurry. {p}It takes a minute to remember how."),
                L("* Still not in a hurry."), "market_sat");
            BenchSeat(b1, 180f, 0.45f, "byte");
            Prop(ctx, "MarketPlant1", "pottedPlant", F, new Vector3(4.1f, 0f, -10.4f), 0f, 1f, false, null);
            Prop(ctx, "MarketPlant2", "pottedPlant", F, new Vector3(8.1f, 0f, -10.5f), 40f, 1.1f, false, null);
            Prop(ctx, "MarketCrates", "cardboardBoxClosed", F, new Vector3(13.9f, 0f, -10.2f), 15f, 0.6f, true,
                L("Crates of instant noodles. <color=#FFD84F>BEST BEFORE: SOMEDAY.</color>"), L("* Someday's a while away."));
        }

        // ====================================================================== Concrete Park: the Quiet Garden

        private static void BuildQuietGarden(Ctx ctx, Material concrete, Material concreteDark, Material stone)
        {
            const string F = "furniture";
            // Garden x -6..5, z -7.3..-14. The park's south boundary has a gap at x -2..1 (the gate).
            Block("Bound_South_W", ctx.env, new Vector3(-8.125f, 1f, -7.3f), new Vector3(12.25f, 2f, 0.2f), null, 0);
            Block("Bound_South_E", ctx.env, new Vector3(4.875f, 1f, -7.3f), new Vector3(7.75f, 2f, 0.2f), null, 0);
            Material grass = LitMaterial("Park/M_GardenGrass", new Color(0.24f, 0.42f, 0.25f), 0.1f);
            Material path = LitMaterial("Park/M_GardenPath", new Color(0.62f, 0.58f, 0.5f));
            Material hedge = LitMaterial("Park/M_Hedge", new Color(0.13f, 0.3f, 0.16f));
            Block("GardenGround", ctx.env, new Vector3(-0.5f, -0.1f, -10.65f), new Vector3(11f, 0.2f, 6.9f), grass, ctx.groundLayer);
            for (int i = 0; i < 6; i++)
                Deco("GardenStone", ctx.env, new Vector3(-0.5f + Mathf.Sin(i * 0.9f) * 0.25f, 0.005f, -7.8f - i * 0.5f), new Vector3(0.55f, 0.02f, 0.4f), path, new Vector3(0f, i * 23f, 0f));
            Block("Garden_BoundS", ctx.env, new Vector3(-0.5f, 1f, -14.2f), new Vector3(11.4f, 2f, 0.2f), null, 0);
            Block("Garden_BoundW", ctx.env, new Vector3(-6.1f, 1f, -10.65f), new Vector3(0.2f, 2f, 7f), null, 0);
            Block("Garden_BoundE", ctx.env, new Vector3(5.1f, 1f, -10.65f), new Vector3(0.2f, 2f, 7f), null, 0);
            // Hedges: tall along the far (east) side, knee-high on the camera sides.
            Deco("Hedge_N_W", ctx.env, new Vector3(-4.05f, 0.35f, -7.45f), new Vector3(4.1f, 0.7f, 0.4f), hedge);
            Deco("Hedge_N_E", ctx.env, new Vector3(3.05f, 0.35f, -7.45f), new Vector3(4.1f, 0.7f, 0.4f), hedge);
            Deco("Hedge_E", ctx.env, new Vector3(4.85f, 0.7f, -10.65f), new Vector3(0.45f, 1.4f, 6.9f), hedge);
            Deco("Hedge_S", ctx.env, new Vector3(-0.5f, 0.25f, -14f), new Vector3(11f, 0.5f, 0.35f), hedge);
            Deco("Hedge_W", ctx.env, new Vector3(-5.85f, 0.25f, -10.65f), new Vector3(0.35f, 0.5f, 6.9f), hedge);
            AddStemZone(ctx, "QuietGarden", new Vector3(-0.5f, 1f, -10.65f), new Vector3(11f, 4f, 6.9f), StemMix.Quiet);

            // Two blossom trees on the far side, petals drifting.
            Material bark = LitMaterial("Park/M_Bark", new Color(0.3f, 0.22f, 0.18f));
            Material blossom = LitMaterial("Park/M_Blossom", new Color(1f, 0.72f, 0.82f));
            foreach (Vector3 t in new[] { new Vector3(3.6f, 0f, -12.6f), new Vector3(3.8f, 0f, -8.6f) })
            {
                Deco("TreeTrunk", ctx.props, t + Vector3.up * 1f, new Vector3(0.22f, 2f, 0.22f), bark);
                foreach (Vector3 o in new[] { new Vector3(0f, 2.3f, 0f), new Vector3(0.45f, 2f, 0.3f), new Vector3(-0.4f, 2.1f, -0.25f), new Vector3(0.1f, 2.65f, -0.35f) })
                {
                    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(puff.GetComponent<Collider>());
                    puff.name = "Blossom";
                    puff.transform.SetParent(ctx.props, false);
                    puff.transform.position = t + o;
                    puff.transform.localScale = new Vector3(1.3f, 0.95f, 1.2f);
                    puff.GetComponent<Renderer>().sharedMaterial = blossom;
                }
                Block("TreeCollider", ctx.env, t + Vector3.up * 1f, new Vector3(0.4f, 2f, 0.4f), null, 0);
            }
            Drift(ctx.props, new Vector3(1.5f, 2.6f, -10.6f), new Vector3(6f, 0.5f, 5.5f), ParticleMaterial("Park/M_Petal", new Color(1f, 0.75f, 0.85f)), 7f, 0.07f, 0.02f);
            Drift(ctx.props, new Vector3(-0.5f, 0.9f, -10.8f), new Vector3(9f, 1.2f, 5.5f),
                ParticleMaterial("Park/M_Firefly", new Color(0.95f, 1f, 0.55f)), 3f, 0.05f, -0.005f);

            // The pond.
            Vector3 pond = new Vector3(-0.8f, 0f, -11.2f);
            GameObject koi = PropModel("Prop_KoiPond", ctx.props, pond, 1.25f, 180f);
            if (koi != null)
            {
                GameObject pondHit = Hitbox("PondBody", koi.transform, pond + Vector3.up * 0.25f, new Vector3(3.2f, 0.5f, 3.2f), ctx.interactableLayer);
                Transform lp = FindDeep(koi.transform, "LanternPoint");
                if (lp != null) Pulse(PointLight(koi.transform, lp.position + Vector3.up * 0.1f, new Color(1f, 0.65f, 0.35f), 1.6f, 4f), LightPulse.Mode.Flicker, 0.5f, 0.25f);
                // Paper boats drifting on the water.
                Material receipt = LitMaterial("Park/M_PaperBoat", new Color(0.96f, 0.95f, 0.88f));
                for (int i = 0; i < 3; i++)
                {
                    var boat = new GameObject("PaperBoat" + i);
                    boat.transform.SetParent(ctx.props, false);
                    boat.transform.position = pond + new Vector3(-0.6f + i * 0.55f, 0.21f, 0.4f - i * 0.35f);
                    Deco("Hull", boat.transform, boat.transform.position, new Vector3(0.22f, 0.05f, 0.1f), receipt);
                    GameObject sail = Deco("Sail", boat.transform, boat.transform.position + Vector3.up * 0.06f, new Vector3(0.12f, 0.12f, 0.01f), receipt, new Vector3(0f, 0f, 45f));
                    sail.transform.SetParent(boat.transform, true);
                    var sp = boat.AddComponent<Spinner>();
                    sp.degreesPerSecond = new Vector3(0f, 6f + i * 3f, 0f);
                    sp.bobAmplitude = 0.01f;
                    sp.bobSpeed = 1.2f;
                }
                Inspectable(pondHit, "KoiPond",
                    L("Fish. Real ones, orange and white, going round and round like they've got somewhere to be.",
                      "* A paper boat drifts against the stones. It's folded out of a receipt.",
                      "* Mia made a whole fleet of these once. Sank every single one in the bath. {p}She said they were 'brave'."),
                    L("* The koi keep going round. {p}{small}No hurry. They'll get there.{/small}"),
                    "key:paper_boat;give:megapotion#garden_pond;+openness#garden_pond", null, null, koi.transform);
            }

            // The gate.
            Material iron = LitMaterial("Park/M_Iron", new Color(0.12f, 0.12f, 0.14f), 0.6f);
            GameObject gateGo = Hitbox("GardenGate", ctx.props, new Vector3(-0.5f, 1.05f, -7.35f), new Vector3(3f, 2.1f, 0.12f), ctx.interactableLayer);
            for (int i = 0; i < 13; i++)
                Deco("GateBar", gateGo.transform, new Vector3(-1.9f + i * 0.233f, 1.05f, -7.35f), new Vector3(0.04f, 2.1f, 0.04f), iron);
            Deco("GateArch", ctx.props, new Vector3(-0.5f, 2.42f, -7.35f), new Vector3(3.4f, 0.22f, 0.3f), concreteDark);
            foreach (float y in new[] { 0.25f, 1.9f })
                Deco("GateRail", gateGo.transform, new Vector3(-0.5f, y, -7.35f), new Vector3(3f, 0.06f, 0.06f), iron);
            foreach (float x in new[] { -2.05f, 1.05f })
                Deco("GatePillar", ctx.props, new Vector3(x, 1.2f, -7.35f), new Vector3(0.3f, 2.4f, 0.3f), concreteDark);
            TextMeshPro gSign = WorldSign(ctx.props, "QUIET GARDEN", new Vector3(-0.5f, 2.42f, -7.52f), 180f, new Color(0.75f, 0.85f, 0.7f), 2.2f);
            gSign.rectTransform.sizeDelta = new Vector2(2.6f, 0.3f);
            FitText(gSign);
            DialogueSequence gateSeq = SeqAsset("Park/GardenGate",
                OnlyIf("!key.garden_key", Say(null, "A locked gate. Through the bars: water, and something green. {p}Green! Down here!", "", DialogueSequence.EndNodeId)),
                Say(null, "The brass key turns like it's been waiting for you.", "", "", "garden_opened"));
            Inspectable(gateGo, "GardenGate", L("..."), null, null, null, gateSeq);
            var gGate = gateGo.AddComponent<FlagGate>();
            Set(gGate, "openFlag", "garden_opened");
            Set(gGate, "dissolveColor", new Color(0.7f, 1f, 0.75f));

            // The bench, facing the pond.
            GameObject bench = Prop(ctx, "GardenBench", "bench", F, new Vector3(1.75f, 0f, -11.2f), 90f, 1.15f, true,
                L("{cam=byte}im not going to eat them. {p}{small}im just going to think about it very hard.{/small}",
                  "{cam=two}...you know whats weird. {p}nobody comes here. {p}{small}and its the best place in the whole city.{/small}",
                  "* {cam=close}Maybe that's why."),
                L("* The water ripples. Byte's tail twitches. {p}Nobody eats anybody."), "garden_sat;+closeness#garden_sat", spkByte);
            BenchSeat(bench, 270f, 0.45f, "byte");
            Prop(ctx, "GardenLamp", "lampSquareFloor", F, new Vector3(-4.8f, 0f, -8.2f), 0f, 1.3f, false, null);
            PointLight(ctx.env, new Vector3(-4.8f, 1.2f, -8.2f), new Color(1f, 0.85f, 0.6f), 1.2f, 4f);
            PointLight(ctx.env, new Vector3(-0.5f, 3.2f, -10.8f), new Color(0.65f, 0.75f, 1f), 1.6f, 9f);
            GameObject sign = Block("Prop_GardenRules", ctx.props, new Vector3(-4.3f, 0.5f, -13.5f), new Vector3(0.5f, 0.35f, 0.05f), path, ctx.interactableLayer);
            Deco("RulesPost", ctx.props, new Vector3(-4.3f, 0.2f, -13.5f), new Vector3(0.05f, 0.4f, 0.05f), bark);
            Inspectable(sign, "GardenRules",
                L("GARDEN RULES. {p}1. No phones. {p}2. No hurrying. {p}3. If you find something, it was left for you."),
                L("Rule 3 has been underlined. Twice."));
        }
    }
}
#endif
