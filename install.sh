#!/bin/bash -ex

KSP=~/KSP_Cutdown
BGDIR=$KSP/GameData/BoosterGuidance

mkdir -p $BGDIR
cp ./obj/Debug/BoosterGuidance.dll $BGDIR
cp BoosterGuidance.cfg $BGDIR
cp BoosterGuidance.version $BGDIR]
cp *.png $BGDIR
