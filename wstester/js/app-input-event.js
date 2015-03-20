function setup_storing_input_time(input_selector, hidden_id) {
	jQuery(function($) {
		$(input_selector).bind('propertychange', function(e) {
			if (e.originalEvent.propertyName == 'value')
				$('#' + hidden_id).val(new Date().getTime());
		});
		var propertyChangeUnbound = false;
		$(input_selector).bind('input', function() {
			if (!propertyChangeUnbound) {
				$(input_selector).unbind('propertychange');
				propertyChangeUnbound = true;
			}
			$('#' + hidden_id).val(new Date().getTime());
		});
	});
}
