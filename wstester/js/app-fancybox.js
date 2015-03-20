function get_fancybox_options(w, h) {
    var fbo = {
        maxWidth: w,
        maxHeight: h,
        minWidth: w,
        minHeight: h,
        fitToView: false,
        width: w, //'70%',
        height: h, //'70%',
        autoSize: false,
        closeClick: false,
        openEffect: 'none',
        closeEffect: 'none',
        helpers: { overlay: { opacity: 0.5 } }
    };
    //$(".fancybox").fancybox(fbo);
    return fbo;
}

var fancybox_options = get_fancybox_options(800, 400);