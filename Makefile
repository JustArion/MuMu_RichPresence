install_velopack:
	dotnet tool update -g vpk

velopack: clean build
	vpk pack -u 'MuMu-RichPresence' -v '1.1.1' -e 'MuMu RichPresence Standalone.exe' -o 'velopack' --packTitle 'MuMu - Rich Presence' -p 'bin' --shortcuts 'StartMenuRoot'

clean:
	del /s /q bin
	del /s /q velopack

build:
	git submodule init
	git submodule update
	dotnet publish ./src/MuMu_RichPresence/ --runtime win-x64 --output ./bin/
	
help:
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build           Build the application"
	@echo "  installvpk      Installs the toolset for auto-updates"
	@echo "  velopack        Build the application with auto-updates"
	@echo "  help            Show this help message"