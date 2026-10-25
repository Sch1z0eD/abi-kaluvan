// Site settings. Edit this file on the server when deploying; the rest of the site stays as is.
window.ABI_CONFIG = {
  // Where the ABI Kaluvan Client (client/) listens on the player's PC. The page tries it first;
  // without it, the browser shares the screen and all PC sound itself.
  // Can be overridden for one visit with ?client=ws://localhost:8777 in the address.
  clientUrl: 'ws://localhost:8777',
  // The shared purchase base of the original project (read only)
  baseRemote: 'https://raw.githubusercontent.com/neverh8me/abi-kaluvan/main/library/purchases.json',
};
