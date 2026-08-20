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

    for (const input of pointInputs) {
        input.min = "1";
        input.step = "100";
        input.required = true;
    }

    // Keep the native spinner at 100-point increments, but do not let the
    // browser reject manually typed positive integers just because they are
    // not aligned to that spinner step.
    form.noValidate = true;

    form.addEventListener("submit", event => {
        const originalSteps = pointInputs.map(input => input.step);

        try {
            for (const input of pointInputs) {
                input.step = "1";
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
