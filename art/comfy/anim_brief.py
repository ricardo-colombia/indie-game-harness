"""ANIMATION BRIEF: what the animation system needs from every character drawing. The generator (gen_apose.py), the cutter (doll_cut.py)
and the runtime (PacmanDoll / PacmanDollPose) all follow this one description, so a figure is drawn *for* the walk, not drawn and then animated.

Rig: head, torso, armL, armR, thighL, thighR, shinL, shinR (8 rigid parts per view; art is one painted figure cut along these lines).
Views: front (south), back (north), side (east; west = mirrored). Every view is generated with the SAME costume.
Joints (pivots): neck (head), shoulders (arms, hang from the torso), hips (thighs, at the shirt hem), knees/ankle (shins, at the trouser cuff).
Walk (PacmanDollPose.Walk), what each part must be able to do:
  side  : thighs swing +-22 deg opposite each other, shins fold when the leg comes forward, arms swing fore-aft +-16 deg AGAINST the legs, torso leans, body bobs.
  front/back : arms hang and only foreshorten (they swing toward/away from the camera), feet lift alternately, torso rolls a few degrees. Arms NEVER flap up and down.
So the drawing must show arms HANGING down at the sides (slightly away from the torso so they cut cleanly), not raised. Figures drawn with arms held out are
re-posed by the cutter: doll_cut.py gives every arm a bind-pose 'rest' roll that hangs it down (ARM_RESTING_GAP degrees away from the body)."""

POSE = ("Single full body character on a plain flat cream background, whole figure visible head to feet, drawn for a walk-cycle animation rig. "
        "Standing neutral, weight even on both feet, both arms hanging straight down at the sides, slightly away from the body so the arms do not touch the torso, hands relaxed and closed, "
        "legs a little apart, feet flat on the ground, nothing overlapping the torso, no props held, symmetrical. ")
VIEW = {"front": "seen from the front, facing the camera",
        "back": "seen from directly behind, the back of the head and the back of the shirt are visible, no face visible at all, back to the camera",
        "side": "seen in strict side profile, exactly 90 degrees, the whole body and head turned to the right, the nose pointing right, only one eye visible, the near arm hanging a little forward and the near leg a little forward"}
ARM_RESTING_GAP = 8.0
