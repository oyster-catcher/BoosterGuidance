# Standard Mac KSP install dir
KSP=/Users/${USER}/Library/Application\ Support/Steam/steamapps/common/Kerbal\ Space\ Program
KSP=~/KSP_Cutdown
VER=v1.0.0

.PHONY: all

all: BoosterGuidance-${VER}.zip install

BoosterGuidance-${VER}.zip: ./obj/Release/BoosterGuidance.dll
	cp $< GameData/BoosterGuidance
	cp License.txt GameData/BoosterGuidance
	cp README.md GameData/BoosterGuidance
	rm -f BoosterGuidance-${VER}.zip
	cd GameData; find BoosterGuidance | zip -@ ../BoosterGuidance-${VER}.zip

install: BoosterGuidance-${VER}.zip
	unzip BoosterGuidance-${VER}.zip -d ${KSP}/GameData

clean:
	rm -f BoosterGuidance.dll *.exe *.zip
