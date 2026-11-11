# OpenGenesisLINK Generation 1 (0.9.4.0) - .NET 10 developer commands
# A normal build must regenerate the generated OpenSim projects first.
# Run "make help" for supported targets. Older build tooling is superseded.

SHELL := /bin/sh
DOTNET ?= dotnet
PYTHON ?= python3
CONFIG ?= Release
SOLUTION := OpenSim.sln

.PHONY: help all prebuild check restore build release test smoke clean

.DEFAULT_GOAL := all

help:
	@printf '%s\n' \
	  'OpenGenesisLINK Generation 1 (.NET 10)' \
	  '  make [all|build]   Generate, restore, and build the server' \
	  '  make prebuild      Regenerate projects and retarget to .NET 10' \
	  '  make check         Verify generated project/runtime .NET 10 contracts' \
	  '  make test          Run focused .NET 10 regressions after build' \
	  '  make smoke         Run isolated ROBUST .NET 10 startup smoke' \
	  '  make clean         Clean generated build outputs (not databases)' \
	  '  make help          Show this help'

all: build

prebuild:
	bash runprebuild.sh

check:
	$(PYTHON) tools/gen11/verify_net10.py

restore: prebuild
	$(DOTNET) restore $(SOLUTION)

build: restore
	$(PYTHON) tools/gen11/verify_net10.py
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIG) --no-restore

release: build

test: build
	$(DOTNET) run --configuration $(CONFIG) --project tools/ci/PublicSimulatorVersionRegression/PublicSimulatorVersionRegression.csproj
	$(DOTNET) run --configuration $(CONFIG) --project tools/ci/FlotsamCacheNet10Regression/FlotsamCacheNet10Regression.csproj

smoke: build
	$(PYTHON) tools/gen11/robust_runtime_smoke.py

clean:
	$(DOTNET) clean $(SOLUTION) --configuration $(CONFIG)

# Optional local targets; keep this file untracked.
-include Makefile.local
