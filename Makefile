SHELL := pwsh.exe
.SHELLFLAGS := -Command

install_velopack:
	dotnet tool update -g vpk

velopack: clean build
	vpk pack -u 'MuMu-RichPresence' -v '$(VERSION)' -e 'MuMu RichPresence Standalone.exe' -o 'velopack' --packTitle 'MuMu - Rich Presence' -p 'bin' --shortcuts 'StartMenuRoot'

clean:
	-rm -Recurse -ErrorAction SilentlyContinue bin
	-rm -Recurse -ErrorAction SilentlyContinue velopack

build:
	git submodule init
	git submodule update
	dotnet publish ./src/MuMu_RichPresence/ --runtime win-x64 --output ./bin/
	
help:
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build                 Build the application"
	@echo "  install_velopack      Installs the toolset for auto-updates"
	@echo "  velopack              Build the application with auto-updates"
	@echo "  help                  Show this help message"