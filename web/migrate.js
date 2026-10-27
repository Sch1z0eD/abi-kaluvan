// Settings saved in this browser under the earlier key names (recorded sounds, purchases, language, display
// mode…) move to the Merchant Ear names once, so nothing is lost after the rename. Runs before everything else.
(() => {
  const OLD = [['abi-kaluvan-', 'merchant-ear-'], ['abi-ear-', 'merchant-ear-']];
  try {
    for (const key of Object.keys(localStorage)) {
      for (const [from, to] of OLD) {
        if (!key.startsWith(from)) continue;
        const target = to + key.slice(from.length);
        if (localStorage.getItem(target) === null) localStorage.setItem(target, localStorage.getItem(key));
        localStorage.removeItem(key);
      }
    }
  } catch {}
})();
