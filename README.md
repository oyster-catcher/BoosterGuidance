# BoosterGuidance

## INSTALLATION

Unzip the zip for for BoosterGuidance inside your KSP installations GameData directory. You should then see the directory BoosterGuidance containing a DLL,
this readme file and the License.

## USING

- What it does

BoosterGuidance aims to fly your rocket to your chosen destination SpaceX Falcon 9 style by using boostback, re-entry burn, aerodynamically steered descent and landing burn.
You would enable guidance after selecting your target when you want boostback to commence, ideally out of the atmosphere. You aim your rocket back to the launch site, do a drone
ship or to another landing site all the way from orbit.

- Prerequistes

Your rocket needs to have sufficient thrust to get to your destination, RCS thrusters to change attitude in space and ideally grid fins for steering in the aerodynamic descent.

- What does the BoosterGuidance window show?

The main window shows

- Target: The landing target is shown in latitude and longitude. Edit manually or use the adjusters 

- Pick target: Click to select to target in the main
flight or map view (unfortunately this doesn't seem to work for any terrain, just physical objects like landing pads and runways), or you can set the latitude and longitude manually,
or in the map view set a vessel as the target and its location will be copied to Booster Guidance. This is particularly easy for a drone ship for example

- Set Here: Set the target to the current location

- Target altitude: Altitude of the target!

- Show targets: Toggle wether the target (yellow) and predicted landing (red) are displayed in the flight and map view

- Logging: Logs the actual data and simulated trajectories to file

- Info message: When guidance is enabled this will show
  - The predicted error between the target and the predicted landing point
  - The error in degrees between the desired attitude (pointing direction) of the vessel and the actual attitude
  - Time to landing
  - In [] the time in milliseconds to do each simulation. By default ten of these are down each seconds. More for debugging any slowness

New we see controls for each of the phases of flight. Each has a button so you can force this phase of flight but each phase has rules which means the next phase will
automatically be triggered. In turn.

- Boostback

No controls for this. The rocket will turn to fire its engines to minimize the target error. For return to launch site this will mean aiming back at the launch site.
It will waits to turn with RCS if the target error is low and will reduce throttle to slowly approach when the target error is low.
The next phase, Coasting, will be triggered when the target error is below 20m is the target error has risen significantly and probably can't be reduced further.

- Coasting

The booster is on its way to the landing site in a parabolic trajectory. It will steer retrograde (opposite to the direction of flight).
Once the re-entry burn altitude is reached the re-entry burn will be triggered.

- Re-entry Burn

In the re-entry burn phase the currently enabled engines will be fired full throttle (less at the end) until the velocity is reduced to below the target velocity.
This makes takes a long time if velocity is high or the engine thrust is low. The booster will steer slightly away from retrograde to minimise the target error.
How agressively it does this is determined by the gain. It will limits its maximum angle-of-attack to the angle given in the advanced settings. The default of
20 degrees should be fine.
When the target velocity is reached the engines will be shutdown and the Aero Descent phase will be entered.

- Aero Descent

With no thrust the booster will attempt to steer via grid fins towards the target, trying to minimise the target error. Again how agressively it does this is
determined by the gain. Lower in the thicker atmosphere you may find the gain is too high and large oscillations are caused. You should reduce the gain slider
to the left. Again the maximum angle-of-attack is determined from the advanced settings. The default of 20 degrees should be sufficient but I've seen a video of
the Falcon 9 at perhaps 30 degrees angle-of-attack fairly close to landing. In aero descent velocity will be reduced significantly as the atmosphere thickens.
When the landing burn altitude of reached the landing burn will be enabled.

- Landing Burn

This is the final phase to reduce velocity to zero just when the booster touches the ground.
The enabled altitude is calculated regularly taking into account the thrust of the currently active engines and the current mass of the booster.
If we wish to use specific engines to the landing then enable the engines you wish to wish at some other point in the flight than click Set, and the
number of engines to use will be shown. You can do this for a Falcon 9 landing where 3 engines are used for the re-entry burn but only a single engine for the
landing burn. However, you must balance the mass of the booster with the engines you have, and for a Falcon 9 its needs to have almost no fuel left for this to
be possible at all. Finally there is again a steer gain which determines how aggressively to steer in this phase with the engine enabled.
Calculating how to the steer when travelling fast is difficult because the thrust pushes the booster in once direction and aerodynamic forces push in the other
direction. BoosterGuidance tries to estimate this. Broadly the aerodynamics forces still dominate until the velocity is under 100-200 m/s.

Finally the big important button at the bottom

- Enabled/disable Guidance:  Toggles whether to fly the rocket with guidance or use manual controls. When enabling BoosterGuidance will enable a suitable phase
of flight by checking the rules from Boostback onwards.

## ADVANCED TOPICS


- Advanced topics
  - Logging
  - Changing engines
  - Realism Overhaul
  - Advanced settings
    - Max angle-of-attack
    - Touchdown margin
    - Touchdown speed
    - Engine startup


## CREDITS

I used some code from the KSPTrajectories mod (https://github.com/neuoy/KSPTrajectories) which I used to calculate the aero dynamic forces and also to draw the targets.
I also use code from the MechJeb2 (https://github.com/MuMech/MechJeb2) mod to make a better UI by using the GUI widgets for latitude and longitude plus some others.
