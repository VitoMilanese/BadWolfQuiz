(() => {
    "use strict";

    if (window.badWolfQuizEditorQuestionPriceInputInitialized) {
        return;
    }

    const form = document.querySelector("form.quiz-board-form");
    if (!form) {
        return;
    }

    window.badWolfQuizEditorQuestionPriceInputInitialized = true;

    const pointInputs = Array.from(
        form.querySelectorAll("input.question-points-input[type='number']"));
    if (pointInputs.length === 0) {
        return;
    }

    const language = (document.documentElement.lang || "en")
        .split("-")[0]
        .toLowerCase();
    const validationMessages = {
        en: "Enter a positive whole number.",
        uk: "Введіть додатне ціле число.",
        ru: "Введите положительное целое число.",
        it: "Inserisci un numero intero positivo."
    };
    const invalidPriceMessage =
        validationMessages[language] ?? validationMessages.en;

    for (const input of pointInputs) {
        input.required = true;
        input.addEventListener("input", () => input.setCustomValidity(""));
    }

    // Keep the native spinner at the existing 100-point step. Automatic form
    // validation is replaced below so a manually typed value such as 601 is
    // not rejected only because it is off that spinner grid.
    form.noValidate = true;

    const isPositiveInteger = input => {
        const value = input.valueAsNumber;
        return input.value.trim() !== "" &&
            Number.isFinite(value) &&
            Number.isInteger(value) &&
            value > 0;
    };

    form.addEventListener("submit", event => {
        let firstInvalidPoint = null;

        for (const input of pointInputs) {
            const valid = isPositiveInteger(input);
            input.setCustomValidity(valid ? "" : invalidPriceMessage);
            if (!valid && !firstInvalidPoint) {
                firstInvalidPoint = input;
            }
        }

        if (firstInvalidPoint) {
            event.preventDefault();
            event.stopImmediatePropagation();
            firstInvalidPoint.reportValidity();
            return;
        }

        const originalSteps = pointInputs.map(input => input.step);
        try {
            // Ignore only the 100-point alignment while the browser validates
            // the rest of the form. The native spinner step is restored before
            // any save handler or normal form submission continues.
            for (const input of pointInputs) {
                input.step = "any";
            }

            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopImmediatePropagation();
                form.reportValidity();
            }
        } finally {
            pointInputs.forEach((input, index) => {
                input.step = originalSteps[index];
            });
        }
    }, true);
})();
