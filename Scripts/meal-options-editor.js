(function(window, $){
  if (!window || !$) {
    return;
  }

  var defaults = {
    hiddenInput: null,
    container: null,
    texts: {
      empty: '尚未設定任何選項群組。',
      addGroup: '新增選項群組',
      removeGroup: '刪除此群組',
      confirmDeleteGroup: '確定要刪除此選項群組嗎？',
      groupNamePlaceholder: '群組名稱，例如：飲料加價',
      requiredLabel: '此群組必選',
      multipleLabel: '允許多選',
      maxSelectLabel: '最多可選',
      addOption: '新增選項',
      removeOption: '刪除',
      optionNamePlaceholder: '選項名稱，例如：大杯',
      optionPricePlaceholder: '加價金額 (元，可空)',
      emptyOptionsNotice: '尚未新增選項。',
      invalidJson: '無法解析既有的選項設定，已載入為空白。',
      groupNameRequired: '第 {index} 個選項群組需要填寫名稱。',
      groupOptionsRequired: '「{name}」至少需要一個選項。'
    }
  };

  function normalizeItem(data) {
    var name = '';
    if (data && data.name != null) {
      name = String(data.name);
    }

    var price = 0;
    if (data && data.price != null) {
      if (typeof data.price === 'number') {
        price = isFinite(data.price) ? data.price : 0;
      } else {
        var parsed = parseFloat(data.price);
        price = isFinite(parsed) ? parsed : 0;
      }
    }

    return { name: name, price: price };
  }

  function normalizeGroup(data) {
    var name = data && data.name != null ? String(data.name) : '';
    var required = !!(data && (data.required || data.isRequired));
    var allowMultiple = !!(data && (data.allowMultiple || data.multi || data.multiple || data.allowMulti));

    var rawMax = data ? (data.maxSelect != null ? data.maxSelect :
      (data.maxSelection != null ? data.maxSelection :
      (data.max != null ? data.max :
      (data.limit != null ? data.limit : data.maximum)))) : null;

    var maxSelect = 1;
    if (rawMax != null) {
      var parsedMax = parseInt(rawMax, 10);
      if (isFinite(parsedMax) && parsedMax > 0) {
        maxSelect = parsedMax;
      }
    }

    if (!allowMultiple && maxSelect > 1) {
      allowMultiple = true;
    }

    if (!allowMultiple) {
      maxSelect = 1;
    }

    var rawItems = [];
    if (data) {
      if (Array.isArray(data.items)) {
        rawItems = data.items;
      } else if (Array.isArray(data.options)) {
        rawItems = data.options;
      }
    }

    var items = rawItems.map(normalizeItem);

    return {
      name: name,
      required: required,
      allowMultiple: allowMultiple,
      maxSelect: allowMultiple ? (maxSelect > 0 ? maxSelect : 1) : 1,
      items: items
    };
  }

  function formatPriceForInput(price) {
    if (!isFinite(price) || price === null) {
      return '';
    }
    if (price === 0) {
      return '';
    }
    if (Math.round(price * 100) === price * 100) {
      if (price % 1 === 0) {
        return price.toString();
      }
      return (price.toFixed(2)).replace(/0+$/,'').replace(/\.$/,'');
    }
    return price.toString();
  }

  function create(config) {
    var options = $.extend(true, {}, defaults, config || {});
    var $hidden = options.hiddenInput ? $(options.hiddenInput) : $();
    var $container = options.container ? $(options.container) : $();

    if (!$hidden.length || !$container.length) {
      throw new Error('MealOptionsEditor 需要指定 hiddenInput 與 container。');
    }

    var texts = options.texts || defaults.texts;
    $container.addClass('meal-options-editor');

    var $error = $('<div class="meal-options-editor__error text-danger small" role="alert"/>').hide();
    var $groups = $('<div class="meal-options-editor__groups"/>');
    var $empty = $('<div class="meal-options-editor__empty text-muted small"/>').text(texts.empty);
    var $actions = $('<div class="meal-options-editor__actions"/>');
    var $addGroup = $('<button type="button" class="btn btn-sm btn-outline-primary"/>').text(texts.addGroup);

    $actions.append($addGroup);
    $container.empty().append($error, $groups, $empty, $actions);

    function clearError() {
      $error.text('').hide();
    }

    function showError(message) {
      $error.text(message).show();
    }

    function updateEmptyState() {
      var hasGroups = $groups.children('.meal-options-editor__group').length > 0;
      if (hasGroups) {
        $empty.hide();
      } else {
        $empty.show();
      }
    }

    function buildGroup(groupData) {
      var data = normalizeGroup(groupData || {});
      var $group = $('<div class="meal-options-editor__group"/>');
      var $header = $('<div class="meal-options-editor__group-header"/>').appendTo($group);
      var $nameInput = $('<input type="text" class="form-control form-control-sm meal-options-editor__group-name"/>')
        .attr('placeholder', texts.groupNamePlaceholder)
        .val(data.name)
        .appendTo($header);
      var $removeGroup = $('<button type="button" class="btn btn-sm btn-outline-danger"/>')
        .text(texts.removeGroup)
        .appendTo($header);

      $removeGroup.on('click', function(){
        clearError();
        if (texts.confirmDeleteGroup) {
          if (!window.confirm(texts.confirmDeleteGroup)) {
            return;
          }
        }
        $group.remove();
        updateEmptyState();
      });

      var $settings = $('<div class="meal-options-editor__group-settings"/>').appendTo($group);
      var $requiredWrap = $('<label class="form-check-inline form-check-label meal-options-editor__toggle"/>').appendTo($settings);
      var $requiredInput = $('<input type="checkbox" class="form-check-input meal-options-editor__required"/>')
        .prop('checked', !!data.required)
        .appendTo($requiredWrap);
      $requiredWrap.append(' ' + texts.requiredLabel);

      var $multipleWrap = $('<label class="form-check-inline form-check-label meal-options-editor__toggle"/>').appendTo($settings);
      var $multipleInput = $('<input type="checkbox" class="form-check-input meal-options-editor__multiple"/>')
        .prop('checked', !!data.allowMultiple)
        .appendTo($multipleWrap);
      $multipleWrap.append(' ' + texts.multipleLabel);

      var $maxWrapper = $('<div class="meal-options-editor__max-select"/>').appendTo($settings);
      $('<span class="meal-options-editor__max-label"/>').text(texts.maxSelectLabel).appendTo($maxWrapper);
      var $maxInput = $('<input type="number" class="form-control form-control-sm meal-options-editor__max" min="1"/>')
        .val(data.allowMultiple ? data.maxSelect : 1)
        .appendTo($maxWrapper);

      function syncMaxVisibility() {
        var multi = $multipleInput.is(':checked');
        if (!multi) {
          $maxInput.val(1);
        }
        $maxWrapper.toggleClass('is-hidden', !multi);
      }

      $multipleInput.on('change', syncMaxVisibility);
      syncMaxVisibility();

      var $items = $('<div class="meal-options-editor__items"/>').appendTo($group);

      function ensureItemsPlaceholder() {
        if (!$items.find('.meal-options-editor__items-empty').length) {
          $items.append($('<div class="meal-options-editor__items-empty text-muted small"/>').text(texts.emptyOptionsNotice));
        }
      }

      function addItemRow(itemData) {
        var item = normalizeItem(itemData || {});
        $items.find('.meal-options-editor__items-empty').remove();
        var $row = $('<div class="meal-options-editor__item"/>');
        $('<input type="text" class="form-control form-control-sm meal-options-editor__item-name"/>')
          .attr('placeholder', texts.optionNamePlaceholder)
          .val(item.name)
          .appendTo($row);
        $('<input type="number" step="0.1" class="form-control form-control-sm meal-options-editor__item-price"/>')
          .attr('placeholder', texts.optionPricePlaceholder)
          .val(formatPriceForInput(item.price))
          .appendTo($row);
        var $remove = $('<button type="button" class="btn btn-sm btn-outline-danger meal-options-editor__remove-item"/>')
          .text(texts.removeOption)
          .appendTo($row);
        $remove.on('click', function(){
          clearError();
          $row.remove();
          if (!$items.children('.meal-options-editor__item').length) {
            ensureItemsPlaceholder();
          }
        });
        $items.append($row);
      }

      if (data.items.length) {
        data.items.forEach(function(item){ addItemRow(item); });
      } else {
        ensureItemsPlaceholder();
      }

      var $addOption = $('<button type="button" class="btn btn-sm btn-outline-secondary meal-options-editor__add-option"/>')
        .text(texts.addOption)
        .appendTo($group);

      $addOption.on('click', function(){
        clearError();
        addItemRow({ name: '', price: 0 });
        var $last = $items.children('.meal-options-editor__item').last();
        var $input = $last.find('.meal-options-editor__item-name');
        if ($input.length) {
          $input.trigger('focus');
        }
      });

      return $group;
    }

    function loadFromJson(json) {
      clearError();
      var raw = json;
      if (typeof raw === 'string') {
        raw = raw.trim();
      }
      var groups = [];
      if (raw) {
        try {
          var parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
          if (parsed && Array.isArray(parsed.groups)) {
            groups = parsed.groups;
          } else {
            showError(texts.invalidJson);
          }
        } catch (err) {
          showError(texts.invalidJson);
        }
      }

      if (typeof json === 'string') {
        $hidden.val(json);
      } else if (!raw) {
        $hidden.val('');
      }

      $groups.empty();
      if (groups.length) {
        groups.forEach(function(g){
          $groups.append(buildGroup(g));
        });
      }
      updateEmptyState();
    }

    function toJson() {
      clearError();
      var groupsData = [];
      var hasGroups = false;
      var hasError = false;

      $groups.children('.meal-options-editor__group').each(function(index){
        hasGroups = true;
        var $group = $(this);
        var name = $.trim($group.find('.meal-options-editor__group-name').val());
        var required = $group.find('.meal-options-editor__required').is(':checked');
        var allowMultiple = $group.find('.meal-options-editor__multiple').is(':checked');
        var maxSelect = allowMultiple ? parseInt($group.find('.meal-options-editor__max').val(), 10) : 1;
        if (!allowMultiple) {
          maxSelect = 1;
        }
        if (allowMultiple && (!isFinite(maxSelect) || maxSelect <= 0)) {
          maxSelect = 1;
        }

        var items = [];
        $group.find('.meal-options-editor__item').each(function(){
          var $item = $(this);
          var itemName = $.trim($item.find('.meal-options-editor__item-name').val());
          var priceRaw = $.trim($item.find('.meal-options-editor__item-price').val());
          if (!itemName) {
            return;
          }
          var price = parseFloat(priceRaw);
          if (!isFinite(price)) {
            price = 0;
          }
          items.push({ name: itemName, price: price });
        });

        if (!name) {
          showError(texts.groupNameRequired.replace('{index}', index + 1));
          $group.find('.meal-options-editor__group-name').focus();
          hasError = true;
          return false;
        }

        if (!items.length) {
          showError(texts.groupOptionsRequired.replace('{name}', name));
          $group.find('.meal-options-editor__add-option').focus();
          hasError = true;
          return false;
        }

        if (allowMultiple && maxSelect > items.length) {
          maxSelect = items.length;
        }

        groupsData.push({
          name: name,
          required: required,
          allowMultiple: allowMultiple,
          maxSelect: allowMultiple ? maxSelect : 1,
          items: items
        });
      });

      if (hasError) {
        return null;
      }

      if (!hasGroups) {
        $hidden.val('');
        return '';
      }

      var result = JSON.stringify({ groups: groupsData });
      $hidden.val(result);
      return result;
    }

    $addGroup.on('click', function(){
      clearError();
      var $group = buildGroup({ name: '', required: false, allowMultiple: false, items: [] });
      $groups.append($group);
      updateEmptyState();
      $group.find('.meal-options-editor__group-name').focus();
    });

    updateEmptyState();

    var initialValue = $hidden.val();
    if (initialValue) {
      loadFromJson(initialValue);
    }

    return {
      loadFromJson: loadFromJson,
      toJson: toJson,
      getRawValue: function(){ return $hidden.val(); }
    };
  }

  window.MealOptionsEditor = {
    create: create
  };
})(window, window.jQuery);
