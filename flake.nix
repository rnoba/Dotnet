{
  description = "C# .NET development environment";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-24.11";
  };

  outputs = { self, nixpkgs }:
    let
    system = "x86_64-linux";
    pkgs = nixpkgs.legacyPackages.${system};
    dotnet = pkgs.dotnetCorePackages.combinePackages [
      pkgs.dotnetCorePackages.dotnet_9.sdk
      pkgs.dotnetCorePackages.dotnet_9.runtime
    ];
  in
  {
    lib = {
      mkDotnetShell = { name, extraPackages ? [] }: pkgs.mkShell {
        inherit name;

        packages = with pkgs; [
          dotnet
          omnisharp-roslyn
        ] ++ extraPackages;

        shellHook = ''
          echo "→ ${name} ready"
          echo "   SDK Version : $(dotnet --version)"

         DOTNET_ROOT_REAL=$(dotnet --info 2>/dev/null \
            | grep 'Base Path' \
            | sed 's|.*Base Path:[[:space:]]*||' \
            | sed 's|/sdk/.*||')

          if [ -n "$DOTNET_ROOT_REAL" ]; then
            export DOTNET_ROOT="$DOTNET_ROOT_REAL"
            echo "   DOTNET_ROOT (real) : $DOTNET_ROOT"
          fi

          export DOTNET_CLI_HOME="$PWD/.dotnet"
          export NUGET_PACKAGES="$PWD/.nuget/packages"
          export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
          export DOTNET_CLI_TELEMETRY_OPTOUT=1
          export DOTNET_NOLOGO=1

          mkdir -p "$PWD/.dotnet/tools"
          mkdir -p "$PWD/.nuget/packages"
          export PATH="$PWD/.dotnet/tools:$PATH"

          echo "   Local dotnet home : $PWD/.dotnet"
          echo "   Local NuGet cache : $PWD/.nuget/packages"
          '';
      };
    };

    devShells.${system}.default = self.lib.mkDotnetShell {
      name = "dotnet-shell";
    };
  };
}
