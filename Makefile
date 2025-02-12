build:
	dotnet publish ./src/PlayGames_RichPresence/ --runtime win-x64 --output ./bin/
	
help:
	@echo "Usage: make <target>"
	@echo ""
	@echo "Targets:"
	@echo "  build       Build the application"
	@echo "  help        Show this help message"