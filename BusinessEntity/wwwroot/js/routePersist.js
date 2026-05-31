(function(){
  const KEY = 'be_last_route';
  window.beRoutes = {
    setLastRoute: function(path){
      try { localStorage.setItem(KEY, path || ''); } catch {}
    },
    getLastRoute: function(){
      try { return localStorage.getItem(KEY) || ''; } catch { return ''; }
    },
    clearLastRoute: function(){
      try { localStorage.removeItem(KEY); } catch {}
    }
  };
})();
