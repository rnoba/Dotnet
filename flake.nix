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
        pkgs.dotnetCorePackages.sdk_10_0
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
            echo "${name} ready"
            echo " SDK Version : $(dotnet --version)"
            export DOTNET_ROOT="${dotnet}"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1

            export DOTNET_CLI_HOME="$PWD/.dotnet"
            export NUGET_PACKAGES="$PWD/.dotnet/packages"

            mkdir -p "$PWD/.dotnet/packages"
            mkdir -p "$PWD/.dotnet/tools"

            export PATH="$PATH:$PWD/.dotnet/tools"
          '';
        };
      };

      devShells.${system}.default = self.lib.mkDotnetShell {
        name = "dotnet-shell";
      };
    };
}
