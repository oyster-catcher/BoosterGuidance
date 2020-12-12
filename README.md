# BoosterGuidance

## INSTALLATION

Unzip the zip for for BoosterGuidance inside your KSP installations GameData directory. You should then see the directory BoosterGuidance containing a DLL,
this readme file and the License.

## USING

- What it does

BoosterGuidance aims to fly your booster to your chosen destination SpaceX Falcon 9 style by using boostback, re-entry burn, aerodynamically steered descent and landing burn.
You would enable guidance after selecting your target when you want boostback to commence, ideally out of the atmosphere. It can fly your booster back to the launch site, to a drone
ship or to another landing site all the way from a sub-orbital trajectory.

- Prerequistes

Your booster needs to have sufficient thrust to get to the destination, RCS thrusters to change attitude in space and ideally grid fins for steering in the aerodynamic descent. The main
engine must have thrust vectoring and be throtteable.

- What does the BoosterGuidance window show?

The main window shows

- Target: The landing target is shown in latitude and longitude. Edit manually or use the adjusters .

- Pick target: Click to select to target in the main flight or map view (unfortunately this doesn't seem to work for any terrain, just physical objects like landing pads and runways), or you can set the latitude and longitude manually,
or in the map view set a vessel as the target and its location will be copied to Booster Guidance. This is particularly easy for a drone ship for example

- Set Here: Set the target to the current location

- Target altitude: Altitude of the target!

- Show targets: Toggle whether or not the target (yellow) and predicted landing point (red) are displayed in the flight and map view

- Logging: Logs the actual data and simulated trajectories to files stored in your KSP trajectory (see advanced topics)

- Info message: When guidance is enabled this will show
  - The predicted error between the target and the predicted landing point
  - The error in degrees between the desired attitude (pointing direction) of the vessel and the actual attitude
  - Time to landing
  - In [] the time in milliseconds to do each simulation. By default ten of these are done each seconds. This is more for debugging purposes

New we see controls for each of the phases of flight. Each has a button so you can force this phase of flight but each phase has rules with which the next phases will
automatically be triggered.

- Boostback

No controls for this. The booster will turn to fire its engines to minimize the target error. For return to launch site this will mean aiming back at the launch site.
It will turn using RCS (if enabled) and thrust vectoring to aim correctly. It will then increase the throttle and finally reduce throttle when the target error is getting low for increased accuracy.
The next phase, Coasting, will be triggered when the target error is below 20m or if the target error has risen significantly and probably can't be reduced further.

- Coasting

The booster is on its way to the landing site in a parabolic trajectory, it should initially be rising, reach apogeee and then fall. It will steer retrograde (opposite to the direction of flight).
Once the re-entry burn altitude is reached the Re-entry burn will be triggered.

- Re-entry Burn

In the re-entry burn phase the currently enabled engines will be fired full throttle (less at the end) until the velocity is reduced below the target velocity.
This makes takes a long time if velocity is high or the engine thrust is low. The booster will steer slightly away from retrograde to minimise the target error.
How aggressively it does this is determined by the gain. It will limit its maximum angle-of-attack to the angle given in the advanced settings. The default of
20 degrees should be fine. When the target velocity is reached the engines will be shutdown and the Aero Descent phase will be entered.

- Aero Descent

With no thrust the booster will attempt to steer via grid fins towards the target, trying to minimise the target error. Again how aggressively it does this is
determined by the gain. Lower in the thicker atmosphere you may find the gain is too high and large oscillations are caused. You should reduce the gain slider
to the left or reduce the maximum angle-of-attack. Again the maximum angle-of-attack is determined from the advanced settings. The default of 20 degrees should be
sufficient but I've seen a video of the Falcon 9 at perhaps 30 degrees angle-of-attack fairly close to landing, so heroic maneoveurs can be made. In aero descent
velocity will be reduced significantly as the atmosphere thickens. When the landing burn altitude of reached the landing burn will be enabled.

- Landing Burn

This is the final phase to reduce velocity to zero just when the booster touches the ground.
The enabled altitude is calculated regularly taking into account the thrust of the currently active engines and the current mass of the booster.
If we wish to use specific engines for the landing then enable the engines you wish to use at some other point in the flight than click Set, and the
number of engines to use will be shown. You can do this for a Falcon 9 landing where 3 engines are used for the re-entry burn but only a single engine for the
landing burn. However, you must balance the mass of the booster with the engines you have, and for a Falcon 9 its needs to have almost no fuel left for this to
be possible at all. Finally there is again a steer gain which determines how aggressively to steer in this phase with the engine enabled. If you do this in
Realism Overhaul take care, its very easy for the enable altitude of the landing burn to be calculated earlier in flight since consumption of propellant by later
phases is not simulated. So the predicted mass will be too high until after re-entry burn.
Calculating how which direction to stter when travelling fast is difficult because the thrust pushes the booster in once direction and aerodynamic forces push in the other
direction. BoosterGuidance tries to estimate this but it might get it wrong. Broadly the aerodynamics forces still dominate until the velocity is under 100-200 m/s.

Finally the big important button at the bottom

- Enable/disable Guidance:  Toggles whether to fly the booster with guidance or use manual controls. When enabling BoosterGuidance will enable a suitable phase
of flight by checking the rules from Boostback onwards.

## ADVANCED TOPICS

In the base game you may not need to touch the advanced setting but you probably will in Realism Overhaul.

### Logging

It you enable logging then several files will be written to your KSP installation directory during the flight. They will be named $vesselname.Simulate.$phase.dat
and $vesselname.Actual.dat. Each file will give a full directory for either the simulation forwards in time when entering the phase given or the actual flight
parameters. After the flight you can plot these using

```
./plot.py ~/KSP_install/MyVessel.Simulate.Coasting.dat ~/KSP_install/MyVessel.Actual.dat
```
and the plots will show: velocity, time, altitude, engine acceleration and trajectory. They are interesting to view and help debug why the mod or your flight went
wrong.

 ### Changing engines

In the base game with a powerful engine that can throttle all down to 0% you can do the whole flight with a single engine. But in RO where engines have limited
throttling and will only throttle down to 40-50% will have a choose which engines to use for different phases of flight. BoosterGuidance will simulate the rest
of the flight using the engines you have activated now. If you make changes the simulation will be wrong but may be able to correct.

### Maximum angle-of-attack

In re-entry burn, aerodynamic descent and landing burn the booster can steer away from retrograde but this angle-of-attack is limited to avoid too high forces
on the booster. You can see this angle, by default it is 20 degrees which should generally work well.

### Deploy gear height

The height to deploy the landing gear/legs if deploy landing gear is switched on.

### No steer height

Below a certain height we want to booster to at least make a soft landing even if in the wrong place. Below the height given the booster will no longer steer
towards the target and will just try to make to reduce the horizontal velocity to zero and land vertically.

### Touchdown margin

This is the height above the target where the velocity should be reduced to the touchdown speed. I've found in RO where the engines throttle up and down more
slowly its good to have an extra margin of error. So rather than the default of 10m you might want to use 30m.

### Touchdown speed

This determines how gently you wish to touchdown.

### Engine startup

In RO engines take some time to startup so they are ignited early than before they are needed, but only for the landing burn where this is most crucial. If you
find you hit the ground in RO you might want to increase this and the touchdown margin.

### A typical boostback trajectory

Please look at my video here https://www.youtube.com/watch?v=GMeiB5LbwnY which shows how to do a return to launch site of a Falcon 9 booster. It takes off
from KSC and begins pitching over Eastwards after reaching about 100m/s using 3 engines. At 45km altitude the booster it pitched over 45 degrees and travelling
at 1700m/s (orbital). It coasts up to 70km and then I enable boostback still using all three engines (probably more realistic to use a single engine). The
apoapsis is 167km. Boostback completes in about 40 seconds. The re-entry burn is enabled at 55km when travelling 1260m/s (surface) and the velocity is reduced
to 700m/s by 45km altitude. The thrust is unrealistically high. The landing burn was enabled at 600m above the target using a single engine (this was setup earlier in
the flight). The video shows the output of the logging in the top-left corner. In this example the engine thrust was unreasonably high so the velocity in the
re-entry burn was reduced too quickly which meant the velocity increased more significantly in the aerodynamic descent. I recommend going to flightclub.io to
find realistic simulations of Falcon 9 flight to see if you can match the simulation more precisely.

### Special consideration of Realism Overhaul

I love Realism Overhaul. Its make the challenges of getting to space much more real and you can be surer that what you are doing is more physically possible.
But there are significant challenges, mainly caused by realistic engine capabilities that will affect and annoy you. Heres how to deal with them.

- Limited throttling

The SpaceX Merlin or Raptor engines as well as the Blue origin BE-3/4 engines are one of a few engines that has sufficient thrust and throttling ability to land
a large booster on Earth. You will need to choose just the right number of engines for the landing burn. Well you can choose extra to achieve fast decelaration
but be aware you may have too much thrust to hover or descent slowly (same amount of thrust really) so you may well take off again. You could cut an engine at
the last minute. If you have too much propellant to land a Falcon 9 you will not be able to slow down enough with a single engine, so getting just the right amount of
fuel remaining is critical. I set up action groups to toggle on/off sets of engines if I need different engines for different phases of flight.

- Limited ignitions

This causes problems. You can just switch booster guidance on and off willy nilly! as you will probably run out of ignitions. In particular once the landing burn
is enabled BoosterGuidance will not reduce thrust to zero since this kills the engine and it would be re-ignited. This would also cause delays so it can't be allowed
to happen. So don't waste ignitions.

- Trouble igniting due to unstable propellant

Just before the boostback burn the engines may have been shutdown. To re-ignite then you need the fuel to be stable in the tank, i.e. at the bottom. Its frequently
a problem. If you get an engine window up and will see the propellant status will show things like "Very risky", "Risky", "Stable" and "Very Stable". You want it to
show "Very Stable" before you Enable Guidance. You can do this by spinning the booster slowly with RCS thrusters to start the boostback burn. You will see the status
change to a more stable state.

Have Fun!
Adrian Skilling
(oyster catcher)

## CREDITS

I used some code from the KSPTrajectories mod (https://github.com/neuoy/KSPTrajectories) which I used to calculate the aero dynamic forces and also to draw the targets.
I also use code from the MechJeb2 (https://github.com/MuMech/MechJeb2) mod to make a better UI by using the GUI widgets for latitude and longitude plus some others.
