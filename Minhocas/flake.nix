{
  description = "Minhocas api";
  inputs = {
    root.url = "git+file:///home/rnoba/Lodge/Projects/Dotnet?dir=.";
    nixpkgs.follows = "root/nixpkgs";
  };
  outputs = { self, root, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
    in
    {
      devShells.${system}.default = root.lib.mkDotnetShell {
        name = "minhocas-shell";
        extraPackages = with pkgs; [
        ];
      };
    };
}
