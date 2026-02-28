// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    function toPolygonPoints(sides, radius, centerX, centerY, rotationDeg) {
        var points = [];
        for (var i = 0; i < sides; i++) {
            var angle = ((360 / sides) * i + rotationDeg) * (Math.PI / 180);
            var x = centerX + radius * Math.cos(angle);
            var y = centerY + radius * Math.sin(angle);
            points.push(x.toFixed(2) + "," + y.toFixed(2));
        }

        return points.join(" ");
    }

    function shapeSvgMarkup(shapeName) {
        var s = (shapeName || "").toLowerCase();
        var stroke = "#495057";
        var fill = "none";
        var shapeMarkup = "";

        if (s === "ellipse") {
            shapeMarkup = '<ellipse cx="12" cy="12" rx="7.5" ry="5.5" />';
        } else if (s === "triangle" || s === "round-triangle") {
            shapeMarkup = '<polygon points="12,5 19,18 5,18" />';
        } else if (s === "rectangle" || s === "cut-rectangle") {
            shapeMarkup = '<rect x="4.5" y="6" width="15" height="12" />';
        } else if (s === "round-rectangle" || s === "bottom-round-rectangle") {
            shapeMarkup = '<rect x="4.5" y="6" width="15" height="12" rx="2.5" ry="2.5" />';
        } else if (s === "diamond" || s === "round-diamond") {
            shapeMarkup = '<polygon points="12,4.5 19.5,12 12,19.5 4.5,12" />';
        } else if (s === "pentagon" || s === "round-pentagon") {
            shapeMarkup = '<polygon points="' + toPolygonPoints(5, 7.5, 12, 12, -90) + '" />';
        } else if (s === "hexagon" || s === "round-hexagon" || s === "concave-hexagon") {
            shapeMarkup = '<polygon points="' + toPolygonPoints(6, 7.5, 12, 12, -90) + '" />';
        } else if (s === "heptagon" || s === "round-heptagon") {
            shapeMarkup = '<polygon points="' + toPolygonPoints(7, 7.5, 12, 12, -90) + '" />';
        } else if (s === "octagon" || s === "round-octagon") {
            shapeMarkup = '<polygon points="' + toPolygonPoints(8, 7.5, 12, 12, -90) + '" />';
        } else if (s === "star") {
            shapeMarkup = '<polygon points="12,4.5 14.3,9.1 19.5,9.7 15.7,13.3 16.7,18.5 12,16 7.3,18.5 8.3,13.3 4.5,9.7 9.7,9.1" />';
        } else if (s === "tag" || s === "round-tag") {
            shapeMarkup = '<path d="M5,8 L13,8 L19,12 L13,16 L5,16 Z" />';
        } else if (s === "barrel") {
            shapeMarkup = '<path d="M7,6 C9,4.5 15,4.5 17,6 L17,18 C15,19.5 9,19.5 7,18 Z" />';
        } else if (s === "vee") {
            shapeMarkup = '<path d="M5,7 L12,18 L19,7" />';
        } else if (s === "rhomboid" || s === "right-rhomboid") {
            shapeMarkup = '<polygon points="7,6 19,6 17,18 5,18" />';
        } else {
            shapeMarkup = '<circle cx="12" cy="12" r="7" />';
        }

        return '<svg class="shape-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">' +
            '<g stroke="' + stroke + '" fill="' + fill + '" stroke-width="1.7" stroke-linejoin="round" stroke-linecap="round">' +
            shapeMarkup +
            "</g></svg>";
    }

    function renderShapeItem(data, escape) {
        var text = escape(data.text || "");
        var icon = shapeSvgMarkup(data.value);
        return '<div class="ts-shape-option">' + icon + '<span>' + text + "</span></div>";
    }

    function createTomSelectOptions(select) {
        var options = {
            create: false,
            maxOptions: 2000,
            allowEmptyOption: true,
            hidePlaceholder: false,
            closeAfterSelect: true,
            openOnFocus: true
        };

        if (select.dataset.shapePicker === "true") {
            options.render = {
                option: renderShapeItem,
                item: renderShapeItem
            };
        }

        return options;
    }

    function initTomSelect() {
        if (typeof window.TomSelect === "undefined") {
            return;
        }

        var selects = document.querySelectorAll("select.form-select");
        selects.forEach(function (select) {
            if (select.tomselect) {
                return;
            }

            var ts = new TomSelect(select, createTomSelectOptions(select));

            function getSelectedItemText() {
                if (!ts.items || ts.items.length !== 1) {
                    return "";
                }

                var selectedValue = ts.items[0];
                var selectedOption = ts.options[selectedValue];
                if (!selectedOption || typeof selectedOption.text !== "string") {
                    return "";
                }

                return selectedOption.text;
            }

            function highlightSearchText() {
                if (!ts.control_input) {
                    return;
                }

                ts.control_input.focus();
                ts.control_input.select();
            }

            function focusAndHighlight() {
                var selectedText = getSelectedItemText();
                if (selectedText && !ts.control_input.value) {
                    ts.setTextboxValue(selectedText);
                    ts.wrapper.classList.add("ts-focus-edit");
                }

                if (!ts.isOpen) {
                    ts.open();
                }

                setTimeout(highlightSearchText, 0);
            }

            function exitFocusEditMode() {
                if (!ts.wrapper.classList.contains("ts-focus-edit")) {
                    return;
                }

                ts.wrapper.classList.remove("ts-focus-edit");
                ts.setTextboxValue("");
            }

            ts.on("focus", function () {
                focusAndHighlight();
            });

            ts.on("dropdown_open", function () {
                setTimeout(highlightSearchText, 0);
            });

            ts.on("blur", function () {
                exitFocusEditMode();
            });

            ts.on("change", function () {
                exitFocusEditMode();
            });

            ts.on("item_add", function () {
                exitFocusEditMode();
            });
        });
    }

    document.addEventListener("DOMContentLoaded", initTomSelect);
})();
