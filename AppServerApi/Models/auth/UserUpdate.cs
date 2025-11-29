using System.ComponentModel.DataAnnotations;

// TO DO: Add email and pw update functionality later if needed

public record UserUpdate(
    /*
    [Required(ErrorMessage = "Le pseudo est obligatoire.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Le pseudo doit contenir entre 3 et 20 caractères alphanumériques.")]
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Le pseudo doit contenir uniquement des lettres et des chiffres.")]
    */
    string? Username,

    string? Language,

    string? Email,
    
    /*
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(20, MinimumLength = 4, ErrorMessage = "Le mot de passe doit contenir entre 4 et 20 caractères.")]
    [RegularExpression(@"^[a-zA-Z0-9&^!@#]+$", ErrorMessage = "Le mot de passe doit contenir uniquement des lettres, des chiffres et les caractères spéciaux &^!@#.")]
    */
    string? Password
);