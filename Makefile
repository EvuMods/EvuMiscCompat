.PHONY: fetch-refs build test verify package install clean

ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))
SOLUTION := $(ROOT)/EvuMiscCompat.sln

# A distro package can put a runtime-only `dotnet` ahead of a user-local SDK.
ifneq ($(wildcard $(HOME)/.dotnet/dotnet),)
export PATH := $(HOME)/.dotnet:$(PATH)
export DOTNET_ROOT := $(HOME)/.dotnet
endif

# The environment and the command line win over the gitignored local file.
_GALE_PROFILE_ENV := $(GALE_PROFILE)
-include $(ROOT)/.local.mk
ifneq ($(_GALE_PROFILE_ENV),)
GALE_PROFILE := $(_GALE_PROFILE_ENV)
endif

fetch-refs:
	bash "$(ROOT)/scripts/fetch-refs.sh"

build: fetch-refs
	dotnet build "$(SOLUTION)" -c Release

test: build
	dotnet test "$(SOLUTION)" -c Release --no-build

verify: test

package: build
	bash "$(ROOT)/scripts/package.sh"

install: package
	@test -n "$(GALE_PROFILE)" || { echo "Set GALE_PROFILE in .local.mk or the environment" >&2; exit 1; }
	mkdir -p "$(GALE_PROFILE)/BepInEx/plugins/EvuMiscCompat"
	cp -a "$(ROOT)/dist/EvuMiscCompat/." "$(GALE_PROFILE)/BepInEx/plugins/EvuMiscCompat/"

clean:
	dotnet clean "$(SOLUTION)" -c Release
	rm -rf "$(ROOT)/dist"
