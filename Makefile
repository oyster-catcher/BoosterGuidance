# Standard Mac KSP install dir
KSP=/Users/${USER}/Library/Application\ Support/Steam/steamapps/common/Kerbal\ Space\ Program
# Additional install locations
KSP_CUTDOWN=~/KSP_Cutdown
KSP_RO=~/KSP_RO
VER=v1.0.3a
GAMEDATADEPS=GameData/BoosterGuidance/BoosterGuidance.cfg

.PHONY: all install

all: BoosterGuidance-${VER}.zip install

BoosterGuidance-${VER}.zip: ./obj/Release/BoosterGuidance.dll ${GAMEDATADEPS}
	cp $< GameData/BoosterGuidance
	cp LICENSE GameData/BoosterGuidance
	cp README.md GameData/BoosterGuidance
	rm -f BoosterGuidance-${VER}.zip
	cd GameData; find BoosterGuidance | zip -@ ../BoosterGuidance-${VER}.zip

install: BoosterGuidance-${VER}.zip
	unzip -o BoosterGuidance-${VER}.zip -d ${KSP}/GameData
	unzip -o BoosterGuidance-${VER}.zip -d ${KSP_CUTDOWN}/GameData
	unzip -o BoosterGuidance-${VER}.zip -d ${KSP_RO}/GameData

clean:
	rm -f BoosterGuidance.dll *.exe *.zip
