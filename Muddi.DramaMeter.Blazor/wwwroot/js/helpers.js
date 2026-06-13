window.dramaMeterHelpers = {
    getSvgScale: (svgElement) => {
        const rect = svgElement.getBoundingClientRect();
        return [rect.width, rect.height];
    },
    getBoundingClientRect: (element) => {
        const rect = element.getBoundingClientRect();
        return [rect.left, rect.top];
    }
};
