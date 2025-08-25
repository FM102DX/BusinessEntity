(function(){
  function setCookie(name, value, days){
    try{
      var d = new Date();
      d.setTime(d.getTime() + (days*24*60*60*1000));
      var expires = "expires=" + d.toUTCString();
      document.cookie = name + "=" + encodeURIComponent(value || '') + ";" + expires + ";path=/;samesite=lax";
    }catch{}
  }
  function deleteCookie(name){
    try{ document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:01 GMT;path=/"; }catch{}
  }
  const ID = 'be_selected_space_id';
  const NAME = 'be_selected_space_name';
  window.beSpace = {
    setSelected: function(id, name){ setCookie(ID, id||'', 30); setCookie(NAME, name||'', 30); try { localStorage.setItem(ID, id||''); localStorage.setItem(NAME, name||''); } catch {} },
    clearSelected: function(){ deleteCookie(ID); deleteCookie(NAME); try { localStorage.removeItem(ID); localStorage.removeItem(NAME); } catch {} },
    getSelectedId: function(){ try { return localStorage.getItem(ID) || ''; } catch { return ''; } },
    getSelectedName: function(){ try { return localStorage.getItem(NAME) || ''; } catch { return ''; } }
  };
})();
