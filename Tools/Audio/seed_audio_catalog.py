"""131 independently directed Seed Audio cues; no network, credentials or writes.

Stable IDs and events come from the previous catalog. Variants change material,
gesture, rhythm or orchestration; post-production pitch changes are not new cues.
"""
from collections import Counter
import seed_soundscape as baseline

# Every variant is a separate model performance, not a pitch-shifted derivative.
DESIGNS = {
    'Ready': ('Sparse menu score, glassy synth chords over a restrained sub-bass bed; no climax.', ('Suspended fifths, distant metallic harmonic accents and spacious pauses.',)),
    'Narrative': ('Cinematic instrumental underscore with clear space for dialogue.', ('Act 1: quiet wonder, slow bowed-metal harmonics and widely spaced low synth pulses.', 'Act 2: uneasy recognition, alternating bass notes with delicate mechanical percussion.', 'Act 3: mounting alarm, asymmetrical muted drum patterns and dark restrained string clusters.', 'Act 4: irreversible decision, descending synth ostinato and broad unresolved brass-like pads.')),
    'BattleLow': ('Low-intensity battle score, controlled tension with little percussion.', ('Slow two-note bass pattern, airy granular pads and isolated metal taps.', 'Soft irregular electronic pulses, bowed cymbal textures and long dark chords.')),
    'BattleMedium': ('Moderate battle score, firm propulsion while leaving the midrange open.', ('Tight syncopated electronic drums under short low-string ostinati.', 'Measured tom pattern with plucked synth cells and evolving minor chords.')),
    'BattleHigh': ('Intense cold science-fiction battle score, forceful but intelligible without excessive treble.', ('Driving polyrhythmic industrial drums, low brass-like synth stabs and fast string figures.', 'Heavy half-time impacts, urgent gated bass and rising dissonant orchestral textures.')),
    'Aftermath': ('Restrained aftermath score, energy gently settling into empty space.', ('Long decaying piano-like metallic notes over a thinning low synth chord; sombre, no triumphant fanfare.',)),
    'PauseBed': ('Very quiet pause-menu instrumental bed, suspended time and minimal movement.', ('Soft sustained low fifth with occasional delicate crystalline chord changes, no beat.',)),
    'ResultBed': ('Restrained result-screen instrumental score with a calm sustained ending.', ('Success: resolved dark major harmony, sparse warm brass-like chords and a slow dignified pulse.', 'Incomplete: unresolved descending minor harmony, isolated muted piano notes and a distant low drone.')),
    'Transition': ('Short instrumental transition ending with a clean decaying tail.', ('Discovery: a delicate rising glass arpeggio resolving into a low chord.', 'Warning: two muted industrial impacts and a tight dissonant synth swell.', 'Commitment: a short bowed-metal crescendo into a single controlled bass impact.', 'Aftermath: a descending electronic phrase dissolving into a hollow dark chord.')),
    'SpaceTexture': ('Abstract cinematic space ambience, not literal wind in vacuum; sparse non-musical energy texture.', ('Distant low electromagnetic hum with isolated crystalline flecks and long quiet gaps.', 'Slow pressure-like resonances with faint granular upper harmonics; no rhythmic beat.', 'Very soft broad spectral shimmer with occasional distant metallic groans.')),
    'CabinBed': ('Interior spacecraft equipment ambience, enclosed steel room with quiet short reflections.', ('Calm bridge: stable ventilation, faint instrument fan whirr and sparse relay clicks.', 'Alert bridge: stronger cooling fans, intermittent data relays and low equipment vibration; no alarm beeps.', 'Evacuation compartment: stressed ventilation, loose panel rattles and distant mechanical activity; no voices or footsteps.')),
    'Cruise': ('Steady droplet cruise energy sound, smooth dense hum, no engine rev sequence.', ('Silky magnetic resonance with a soft granular fringe and slow harmonic breathing.', 'Stable low crystalline resonance with faint layered electrical shimmers.')),
    'BoostLoop': ('Steady high-speed droplet boost energy, clearly denser and more urgent than cruise.', ('Focused electrical roar with tightly textured upper harmonics and stable low body.', 'Broad resonant plasma rush with a subtle rapid mechanical tremolo; no discrete starts.')),
    'BoostEnter': ('Droplet boost engages: immediate clean onset, energy gathers then settles into a short tail.', ('Compact magnetic snap followed by a thick accelerating energy rush.', 'Crystalline spark followed by several converging resonant streams.', 'Low pressure punch opening into a textured plasma sweep.')),
    'BoostRelease': ('Boost disengages: energy rapidly unwinds without an impact or explosion.', ('Narrow electrical hiss collapsing into a small resonant click.', 'Layered crystalline resonance gently separates and fades.', 'Dense low energy whoosh breaks into a brief granular afterglow.')),
    'Brake': ('Controlled hard braking of an indestructible droplet; energetic deceleration, no damage.', ('Smooth reverse energy sweep with a compressed low-end thump.', 'Tight magnetic shudder followed by a brief dry electrical release.', 'Broad resonant pressure wash closing sharply with fine crystal flickers.')),
    'Turn': ('Very short executed sharp-turn energy cue, fast lateral motion then silence.', ('Narrow silky energy whip with a crisp magnetic edge.', 'Granular crystalline swish with a double resonant flick.', 'Compact low pressure flick with a fine electrical trailing spray.')),
    'Recover': ('Droplet smoothly recovers forward motion after braking.', ('A soft magnetic latch releases into a short smooth energy swell.', 'Two delicate crystalline ripples merge into a stable low resonance.')),
    'Laser': ('Single ship-mounted science-fiction laser discharge; sharp attack, short energy body, clean decay.', ('Needle-like electrical crack and a dry narrow beam hiss.', 'Heavy capacitor snap with a compact resonant plasma burst.', 'Fine crystalline chirr with a brief granular trailing spray.', 'Double-stage magnetic click then a broad hot energy sweep.', 'Tight metallic ignition and a short textured beam buzz.', 'Bass-supported discharge with tiny electrical arcs in the tail.')),
    'Reflect': ('Laser contacts the indestructible droplet and ricochets; no hurt sound or voice.', ('Bright crystalline contact ping and a narrow departing energy streak.', 'Dense magnetic snap followed by two delicate splinter-like glints.', 'Glassy skimming impact with a brief liquid-metal resonant tail.', 'Hard chrome tick opening into a short electrical scatter.', 'Compact bell-like impact immediately followed by a dry sideways beam hiss.', 'Sharp energy crack with a smooth resonant rebound and no debris.')),
    'Penetration': ('Droplet punches through a ship hull: forceful metal rupture with clear attack and debris decay.', ('Thick steel plate tears inward, deep panel resonance and scattered heavy fragments.', 'Layered alloy plating shears successively with several crisp jagged snaps.', 'Ribbed bulkhead bends, breaks and rings with a few light tumbling shards.', 'Dense armoured hull bursts with a short low impact and gritty metallic spray.', 'Thin outer skin rips into a hollow structural beam fracture and falling scrap.', 'Machined metal deck ruptures with a compressed strike and long bent-panel groan.')),
    'LaserBreach': ('A laser penetrates a ship hull: hot energy contact, sizzling metal rupture and short decay.', ('Fine piercing crack, molten steel sizzle and one small ringing fragment.', 'Heavy energy punch through thick alloy with a granular scorched tail.', 'Rapid heated panel fracture, several sharp shards and a brief electrical arc.')),
    'Explosion': ('Single large spacecraft explosion: immediate impact, deep expanding body, varied metal debris fading fully.', ('Tight reactor punch followed by two overlapping rolling low-frequency booms.', 'Broad hull detonation with a rough tearing middle and scattered steel fragments.', 'Dense muffled core burst opening into a wide resonant pressure decay.', 'Sharp initial electrical crack, a heavy low boom and smaller delayed metal snaps.', 'Large hollow hangar rupture with tumbling heavy plates and diminishing grit.', 'Layered structural collapse under a short violent bass impact and sparse long metal rings.')),
    'Reactor': ('Unstable reactor energy warning texture, continuous mechanical/electrical strain without explosion.', ('Irregular low magnetic surges with sparse tiny arcing cracks.', 'Uneven resonant turbine shudder and dry capacitor rattles.', 'Tense dense electrical buzz with intermittent hollow pressure knocks.')),
    'RetreatEngine': ('A retreating spacecraft engine under steady heavy thrust, sustained mechanical energy.', ('Large turbine rumble with slow metallic vibration and stable exhaust body.', 'Electric drive whine layered with soft repeating power-converter pulses.', 'Broad plasma propulsion roar with rattling nozzle plates and a low resonant core.')),
    'Escape': ('Brief distant spacecraft departure cue ending cleanly; no explosion.', ('Compact receding propulsion flare with a fine electrical wake.', 'Soft vacuum-like pressure release and one fading metallic resonance.')),
    'DeckSteps': ('A few isolated human boot footfalls on a spacecraft deck; only footsteps, no speech or breathing.', ('Measured heavy boots on thick steel grating, four distinct steps with short room reflections.', 'Hurried light boots over ribbed alloy flooring, six uneven steps and one heel scuff.', 'Careful rubber-soled boots across a smooth metal deck, three quieter steps and a soft pivot.')),
    'Console': ('Brief spacecraft console interaction, tactile hardware and a small electronic response.', ('Two firm mechanical key presses with a tiny dry confirmation chirp.', 'A toggle lever clicks into place followed by a short relay chatter.', 'Several soft membrane key taps followed by a low clean status tone.')),
    'Hatch': ('Motorised spacecraft hatch action in a steel interior, clear mechanism and seal detail.', ('Heavy sliding hatch opens: latch release, short motor travel and cushioned stop.', 'Bulkhead door closes: brief hydraulic movement, rubber seal compression and locking clunk.', 'Light maintenance hatch unlocks and folds back with two hinge clicks.')),
    'Alarm': ('Restrained instrumental electronic ship alarm pattern, clear pauses between calls; no spoken warning.', ('Two separated low resonant pulses, regular slow repetition.', 'Three short clipped electronic beeps with a long recovery gap.', 'Alternating hollow mechanical tones with a soft relay click before each pair.')),
    'Connect': ('Very short radio link connection sound, isolated and dry.', ('A soft relay click followed by a short rising electronic pair.', 'A compact digital handshake trill ending in a clean click.', 'A tiny static flutter settling into one muted confirmation pip.')),
    'Disconnect': ('Very short radio link interruption ending abruptly in silence.', ('A relay snap cutting a brief narrow-band hiss.', 'A broken digital chirp followed by one dry click.', 'A small static crackle burst rapidly collapsing to silence.')),
    'Interference': ('Brief isolated communications interference detail; no intelligible voices, music or speech.', ('A dry burst of fine radio static with three tiny clicks.', 'A broken digital packet stutter with small silent gaps.', 'A narrow-band electrical crackle that briefly swells and vanishes.', 'A low relay pop followed by a rough granular signal dropout.')),
    'Focus': ('Tiny restrained interface focus sound, precise attack and no ringing tail.', ('One soft ceramic-like tick with a faint electronic glint.', 'One muted tactile switch tap with a very short glass accent.')),
    'Confirm': ('Short clear interface confirmation, soft and restrained.', ('Two compact crystalline pips with the second slightly fuller.', 'A tactile click followed by a brief resolved electronic chord.')),
    'Back': ('Short interface return cue, calm and understated.', ('A soft reverse glass ripple ending in a dry tap.', 'Two brief descending electronic notes with no sustained tail.')),
    'Toggle': ('Tiny interface toggle acknowledgement with distinct switch action.', ('A dry miniature relay click with a warm digital pip.', 'A soft snap switch followed by a delicate double electronic tick.')),
    'Slider': ('Extremely short quiet interface slider increment, safe for repeated interaction.', ('A single soft granular digital tick.', 'A single muted ceramic contact with a tiny electrical glint.')),
    'Pause': ('Short pause interface cue, clean ending.', ('A compact two-note electronic phrase gently closes into a muted click.',)),
    'Resume': ('Short resume interface cue, clean ending.', ('A light latch click opens into two soft ascending crystalline pips.',)),
    'Skip': ('Brief narrative-skip interface cue with a clean tail.', ('A delicate forward energy swish ends with a single resolved electronic pip.',)),
    'Restart': ('Brief restart interface cue, resetting and beginning anew.', ('A small descending digital ripple followed by a clear low confirmation pulse.',)),
    'History': ('Quiet communications-history panel interface feedback.', ('Opening: a soft paperless electronic flutter followed by one glass tick.', 'Closing: a brief muted digital fold ending with a tactile click.')),
    'Boundary': ('Clear restrained arena-boundary warning, instrumental only.', ('Two spaced hollow electronic pulses with a short dry decay.', 'A low warning pip followed by a small rough-edged digital knock.')),
    'Return': ('Safe-return feedback after arena recovery, reassuring but not celebratory.', ('A smooth short magnetic sweep resolving into two soft electronic notes.', 'A muted tactile impact followed by a small warm crystalline chord.')),
    'TimeWarning': ('Mission time-threshold cue, concise and attention-getting without panic.', ('Sixty seconds: two measured low electronic chimes separated by a short pause.', 'Thirty seconds: three tighter digital pulses with a dry metallic accent.')),
    'Countdown': ('Very short final-countdown tick, clear rhythm when repeated once per second.', ('A single dry rounded electronic tick with a faint low body.', 'A compact double-tap digital tick with immediate decay.')),
    'Combo': ('Short score-multiplier increase cue; distinct rhythmic pattern and clean tail.', ('First tier: one crisp electronic pip followed by a soft metallic tap.', 'Second tier: a quick pair of crystalline notes over a tiny bass pulse.', 'Third tier: three tightly grouped plucked synth notes resolving together.')),
    'ComboCap': ('Short maximum-multiplier feedback, controlled energy without a victory fanfare.', ('A compact four-note crystalline flourish ending in one firm electronic chord.',)),
    'ResultSting': ('Short instrumental mission-result landing with a fully decaying tail.', ('Cleared: restrained resolved brass-like synth chord and one deep dignified impact.', 'Fleet escaped: incomplete descending glass phrase over a hollow low resonance.', 'Timeout: two muted clock-like metal contacts followed by an unresolved dark chord.')),
}

GROUPS = {
    'music': ('Music', 110, 0.0), 'ambience': ('Ambience', 150, 0.0),
    'flight': ('Flight', 100, 0.18), 'combat': ('Combat', 75, 0.05),
    'cabin': ('Communications', 85, 0.15), 'ui': ('Interface', 35, 0.08),
    'mission': ('Interface', 45, 0.3),
}


def catalog():
    """Return the complete authoring plan without reading keys or writing files."""
    previous = baseline.catalog()
    family_by_id = {f'{name}_{n:02}': name for _, name, count, *_ in baseline.FAMILIES
                    for n in range(1, count + 1)}
    cues = []
    for original in previous['cues']:
        item = {key: value for key, value in original.items() if key != 'resource'}
        family = family_by_id[item['id']]
        description, variations = DESIGNS[family]
        variant = variations[item['variant'] - 1]
        group, priority, cooldown = GROUPS[item['category']]
        duration = item['targetSeconds']
        if item['loop'] and item['category'] in ('music', 'ambience'):
            duration = (60, 75, 90)[(item['variant'] - 1) % 3]
        if family in ('Reactor', 'RetreatEngine'):
            priority = 50
        if family == 'Boundary':
            cooldown = 4.0
        elif family == 'Return':
            cooldown = 1.0
        elif family == 'Countdown':
            cooldown = 0.9
        envelope = ('Maintain a consistent sound bed from beginning to end with slow natural variation, '
                    'no opening hit or final cadence; suitable for seamless crossfaded looping.'
                    if item['loop'] else 'One self-contained event or specified short sequence; '
                    'clear attack, natural decay, quiet clean tail; no abrupt truncation or long dead air.')
        excluded = ('No vocals, singing, humming, speech, narration, lyrics or spoken prompt. '
                    'No clipping, harsh distortion or unrelated scene sounds.')
        if item['category'] != 'music':
            excluded += ' No music, melody or soundtrack.'
        prompt = (f'Create original cold cinematic science-fiction game audio, approximately {duration:g} seconds. '
                  f'{description} {variant} {envelope} {excluded}')
        item.update(family=family, provider='seed-audio', model='seed-audio-1.0',
                    targetSeconds=duration, text_prompt=prompt, variantDirection=variant,
                    mixerGroup=group, priority=priority, cooldownSeconds=0.0 if item['loop'] else cooldown,
                    status='planned_not_generated', source='Seed Audio 1.0 original generation',
                    postprocessRecipe=None, subjectiveStatus='not_listened',
                    implementationStatus='not_integrated', maximumRequestCostCNY=2)
        cues.append(item)
    assert len(cues) == 131 and len({cue['id'] for cue in cues}) == 131
    assert len({cue['text_prompt'] for cue in cues}) == 131
    return dict(style=previous['style'], nonSpeechTarget=131, additionalVoiceLimit=24,
                budgetAFP=20000, cashBudget=60, cashCurrency='CNY',
                requiredGate=previous['requiredGate'],
                priorityConvention='Unity AudioSource priority: 0 highest, 256 lowest',
                endpoint='https://openspeech.bytedance.com/api/v3/tts/create',
                rateCNYPerMinute=1, categories=dict(Counter(c['category'] for c in cues)), cues=cues)
