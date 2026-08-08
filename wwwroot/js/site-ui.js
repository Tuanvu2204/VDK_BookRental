// VDK BookRental - site-ui.js
// Helper for small UI behaviors: mobile nav toggle, toast helper fallback
(function(){
  'use strict';

  window.VDKUI = {
    ready: function(cb){
      if(document.readyState==='loading'){
        document.addEventListener('DOMContentLoaded',cb,{once:true});
      } else { cb(); }
    },
    toJson: function(obj){ try { return JSON.stringify(obj); } catch(e) { return '{}'; } }
  };

  VDKUI.ready(function(){
    // Basic accessible toggles can be added here later
    console.debug('VDK UI ready');
  });
})();
