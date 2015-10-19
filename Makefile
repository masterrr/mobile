help:
	@echo "Available targets:"
	@echo " setup - links githooks, inits & updates submodules"
	@echo " clean - remove all build files and reset Build.cs file"
	@echo " format - formats all source files according to our coding convention"

clean:
	rm -rf */{bin,obj}

setup:
	# Link hooks
	ln -fs ../../.githooks/pre-commit.sh .git/hooks/pre-commit
	# Get submodules
	git submodule init
	git submodule update
	# Ignore Build.cs changes
	git update-index --assume-unchanged Phoebe/Build.cs

format:
	@astyle --options=.astylerc --formatted --suffix=none $$(find Joey Ross Phoebe Emma Chandler Tests -type f -name '*.cs')
