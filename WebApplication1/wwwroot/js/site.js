// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener("DOMContentLoaded", function () {
    const paginationContainer = document.querySelector(".pagination");
    const pageButtons = paginationContainer.querySelectorAll("button");
    let currentPage = parseInt(document.querySelector(".active").dataset.page);
    const totalPages = parseInt(paginationContainer.querySelectorAll("[data-page]").length - 2);

    
    function updatePagination() {
        pageButtons.forEach(button => button.classList.remove("active"));
        const activeButton = paginationContainer.querySelector(`button[data-page="${currentPage}"]`);
        if (activeButton) activeButton.classList.add("active");

        paginationContainer.querySelector(".prev-btn").disabled = currentPage === 1;
        paginationContainer.querySelector(".next-btn").disabled = currentPage === totalPages;
    }

    
    pageButtons.forEach(button => {
        button.addEventListener("click", function () {
            const page = this.getAttribute("data-page");
            if (page === "prev" && currentPage > 1) currentPage--;
            else if (page === "next" && currentPage < totalPages) currentPage++;
            else if (!isNaN(parseInt(page))) currentPage = parseInt(page);

            updatePagination();
            loadPageData(currentPage);
        });
    });

    
    function loadPageData(page) {
        console.log(`Loading data for page ${page}`);
        
        window.location.href = `?page=${page}`; 
    }

    updatePagination();
});
