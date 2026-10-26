// Logbook: lightweight client-side search for each page's currently loaded tables/cards.
(function () {
  function normalize(value) {
    return (value || "").toString().toLocaleLowerCase("th-TH").normalize("NFKC").trim();
  }

  document.querySelectorAll("[data-filter-target]").forEach(function (input) {
    const targetSelector = input.getAttribute("data-filter-target");
    if (!targetSelector) return;
    const elements = Array.from(document.querySelectorAll(targetSelector));
    const countTarget = input.getAttribute("data-filter-count");
    const countElement = countTarget ? document.querySelector(countTarget) : null;

    function filterItems() {
      const query = normalize(input.value);
      let visible = 0;
      elements.forEach(function (element) {
        const haystack = normalize(element.innerText || element.textContent);
        const match = !query || haystack.includes(query);
        element.hidden = !match;
        if (match) visible++;
      });
      if (countElement) countElement.textContent = visible.toLocaleString("th-TH") + " รายการ";
    }
    input.addEventListener("input", filterItems);
    filterItems();
  });

  const toggle = document.querySelector("[data-sidebar-toggle]");
  const sidebar = document.querySelector(".sidebar");
  if (toggle && sidebar) toggle.addEventListener("click", () => sidebar.classList.toggle("mobile-open"));
})();
