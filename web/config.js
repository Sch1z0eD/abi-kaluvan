// Site settings. Edit this file on the server when deploying; the rest of the site stays as is.
window.ABI_CONFIG = {
  // Where the Merchant Ear Client (client/) listens on the player's PC. The page tries it first;
  // without it, the browser shares the screen and all PC sound itself.
  // Can be overridden for one visit with ?client=ws://localhost:8777 in the address.
  clientUrl: 'ws://localhost:8777',
  // An online purchase base (JSON with { purchases: [...] }) merged with the one in library/purchases.json; '' — none
  baseRemote: '',
};
