angular.module("umbraco").controller("DynamicDropdownController",
    function ($scope, $http, notificationsService) {

        $scope.loading = true;
        $scope.options = [];

        var category = $scope.model.config.category;

        function loadOptions() {
            $http.get("/api/ContentApi/dropdown-options?category=" + category)
                .then(function (response) {
                    $scope.options = response.data.map(function (item) {
                        return {
                            id: item.key,
                            value: item.key,
                            name: item.value // Display name
                        };
                    });

                    $scope.loading = false;

                    if (!$scope.model.value && $scope.options.length > 0) {
                        $scope.model.value = $scope.options[0].value;
                    }
                })
                .catch(function (error) {
                    notificationsService.error("Error", "Failed to load dropdown options");
                    $scope.loading = false;
                });
        }

        loadOptions();
    });